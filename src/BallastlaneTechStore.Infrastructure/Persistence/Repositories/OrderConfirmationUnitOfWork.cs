using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace BallastlaneTechStore.Infrastructure.Persistence.Repositories;

// Single transaction: conditional product decrements + order header update + items
// replace. If any decrement fails the version check we throw and the whole tx rolls back.
public sealed class OrderConfirmationUnitOfWork : IOrderConfirmationUnitOfWork
{
    private readonly NpgsqlDataSource _ds;
    public OrderConfirmationUnitOfWork(NpgsqlDataSource ds) => _ds = ds;

    public async Task ConfirmAsync(Order order, IReadOnlyDictionary<Guid, int> expectedRowVersions, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        foreach (var line in order.Items)
        {
            var version = expectedRowVersions[line.ProductId];
            await using var dec = conn.CreateCommand();
            dec.Transaction = tx;
            dec.CommandText = @"update products
                                   set stock_on_hand = stock_on_hand - $2,
                                       row_version   = row_version + 1,
                                       updated_at    = now()
                                 where id = $1 and row_version = $3 and stock_on_hand >= $2;";
            dec.Parameters.Add(new NpgsqlParameter { Value = line.ProductId });
            dec.Parameters.Add(new NpgsqlParameter { Value = line.Quantity });
            dec.Parameters.Add(new NpgsqlParameter { Value = version });
            var rows = await dec.ExecuteNonQueryAsync(ct);
            if (rows != 1)
            {
                // Either someone confirmed first (version moved) or stock dipped below
                // our requested quantity. Either way: out of stock from this caller's
                // point of view.
                await using var probe = conn.CreateCommand();
                probe.Transaction = tx;
                probe.CommandText = "select stock_on_hand from products where id = $1;";
                probe.Parameters.Add(new NpgsqlParameter { Value = line.ProductId });
                var available = (int?)await probe.ExecuteScalarAsync(ct) ?? 0;
                throw new OutOfStockException(line.ProductId, line.Quantity, available);
            }
        }

        await using (var head = conn.CreateCommand())
        {
            head.Transaction = tx;
            head.CommandText = @"update orders set status=$2, subtotal=$3, tax=$4, total=$5, updated_at=$6 where id=$1;";
            head.Parameters.Add(new NpgsqlParameter { Value = order.Id });
            head.Parameters.Add(new NpgsqlParameter { Value = (short)order.Status });
            head.Parameters.Add(new NpgsqlParameter { Value = order.Subtotal });
            head.Parameters.Add(new NpgsqlParameter { Value = order.Tax });
            head.Parameters.Add(new NpgsqlParameter { Value = order.Total });
            head.Parameters.Add(new NpgsqlParameter { Value = order.UpdatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            await head.ExecuteNonQueryAsync(ct);
        }

        // Reprice persisted: replace the items with the snapshotted prices.
        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "delete from order_items where order_id = $1;";
            del.Parameters.Add(new NpgsqlParameter { Value = order.Id });
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var item in order.Items)
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "insert into order_items (id, order_id, product_id, quantity, unit_price_snapshot) values ($1,$2,$3,$4,$5);";
            ins.Parameters.Add(new NpgsqlParameter { Value = item.Id });
            ins.Parameters.Add(new NpgsqlParameter { Value = order.Id });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.ProductId });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.Quantity });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.UnitPriceSnapshot });
            await ins.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}

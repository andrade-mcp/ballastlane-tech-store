using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using Npgsql;
using NpgsqlTypes;

namespace BallastlaneTechStore.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly NpgsqlDataSource _ds;
    public OrderRepository(NpgsqlDataSource ds) => _ds = ds;

    public async Task<string> NextOrderNumberAsync(DateTime nowUtc, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select nextval('order_number_seq');");
        var n = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return $"ORD-{nowUtc:yyyyMMdd}-{n:D4}";
    }

    public async Task AddAsync(Order o, CancellationToken ct)
    {
        const string sql = @"insert into orders (id, number, customer_id, status, subtotal, tax, total, owner_id, created_at, updated_at)
                             values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);";
        await using var cmd = _ds.CreateCommand(sql);
        BindHeader(cmd, o);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateHeaderAsync(Order o, CancellationToken ct)
    {
        const string sql = @"update orders set status=$4, subtotal=$5, tax=$6, total=$7, updated_at=$10 where id=$1;";
        await using var cmd = _ds.CreateCommand(sql);
        BindHeader(cmd, o);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReplaceItemsAsync(Order o, CancellationToken ct)
    {
        // Items are owned by the order; the simplest correct implementation is wipe-and-replace
        // inside one transaction. Performance is fine for orders of typical size (<50 lines).
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "delete from order_items where order_id = $1;";
            del.Parameters.Add(new NpgsqlParameter { Value = o.Id });
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var item in o.Items)
        {
            await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "insert into order_items (id, order_id, product_id, quantity, unit_price_snapshot) values ($1,$2,$3,$4,$5);";
            ins.Parameters.Add(new NpgsqlParameter { Value = item.Id });
            ins.Parameters.Add(new NpgsqlParameter { Value = o.Id });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.ProductId });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.Quantity });
            ins.Parameters.Add(new NpgsqlParameter { Value = item.UnitPriceSnapshot });
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("delete from orders where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        Order? order = null;
        await using (var head = conn.CreateCommand())
        {
            head.CommandText = SelectHeader + " where id = $1;";
            head.Parameters.Add(new NpgsqlParameter { Value = id });
            await using var r = await head.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) order = MapHeader(r, Array.Empty<OrderItem>());
        }
        if (order is null) return null;

        var lines = new List<OrderItem>();
        await using (var items = conn.CreateCommand())
        {
            items.CommandText = "select id, order_id, product_id, quantity, unit_price_snapshot from order_items where order_id = $1 order by id;";
            items.Parameters.Add(new NpgsqlParameter { Value = id });
            await using var r = await items.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lines.Add(OrderItem.Hydrate(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetInt32(3), r.GetDecimal(4)));
        }
        return Order.Hydrate(order.Id, order.Number, order.CustomerId, order.Status,
                             order.Subtotal, order.Tax, order.OwnerId, order.CreatedAt, order.UpdatedAt, lines);
    }

    public async Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        var (where, ps) = BuildWhere(status, customerId);
        var sql = SelectHeader + where + $" order by created_at desc offset {skip} limit {take};";
        await using var cmd = _ds.CreateCommand(sql);
        foreach (var p in ps) cmd.Parameters.Add(p);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Order>();
        while (await r.ReadAsync(ct)) list.Add(MapHeader(r, Array.Empty<OrderItem>()));
        return list;
    }

    public async Task<int> CountAsync(OrderStatus? status, Guid? customerId, CancellationToken ct)
    {
        var (where, ps) = BuildWhere(status, customerId);
        await using var cmd = _ds.CreateCommand("select count(*) from orders" + where);
        foreach (var p in ps) cmd.Parameters.Add(p);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyDictionary<OrderStatus, (int Count, decimal Total)>> SummariseAsync(CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select status, count(*), coalesce(sum(total), 0) from orders group by status;");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var dict = new Dictionary<OrderStatus, (int, decimal)>();
        while (await r.ReadAsync(ct))
        {
            var s = (OrderStatus)r.GetInt16(0);
            dict[s] = (Convert.ToInt32(r.GetInt64(1)), r.GetDecimal(2));
        }
        return dict;
    }

    private const string SelectHeader = "select id, number, customer_id, status, subtotal, tax, owner_id, created_at, updated_at from orders";

    private static (string where, List<NpgsqlParameter> ps) BuildWhere(OrderStatus? status, Guid? customerId)
    {
        var ps = new List<NpgsqlParameter>();
        var clauses = new List<string>();
        if (status is { } s)
        {
            ps.Add(new NpgsqlParameter { Value = (short)s });
            clauses.Add($"status = ${ps.Count}");
        }
        if (customerId is { } cid)
        {
            ps.Add(new NpgsqlParameter { Value = cid });
            clauses.Add($"customer_id = ${ps.Count}");
        }
        return clauses.Count == 0 ? ("", ps) : (" where " + string.Join(" and ", clauses), ps);
    }

    private static void BindHeader(NpgsqlCommand cmd, Order o)
    {
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.Id });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.Number });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.CustomerId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (short)o.Status });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.Subtotal });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.Tax });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.Total });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.OwnerId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.CreatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter { Value = o.UpdatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
    }

    private static Order MapHeader(NpgsqlDataReader r, IEnumerable<OrderItem> lines) => Order.Hydrate(
        r.GetGuid(0), r.GetString(1), r.GetGuid(2),
        (OrderStatus)r.GetInt16(3),
        r.GetDecimal(4), r.GetDecimal(5),
        r.GetGuid(6), r.GetDateTime(7), r.GetDateTime(8), lines);
}

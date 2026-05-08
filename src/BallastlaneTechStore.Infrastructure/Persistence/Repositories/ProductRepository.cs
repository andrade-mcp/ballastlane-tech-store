using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using Npgsql;
using NpgsqlTypes;

namespace BallastlaneTechStore.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    // Below this threshold the dashboard / UI flag a product as "low stock".
    private const int LowStockThreshold = 5;

    private readonly NpgsqlDataSource _ds;
    public ProductRepository(NpgsqlDataSource ds) => _ds = ds;

    public async Task AddAsync(Product p, CancellationToken ct)
    {
        const string sql = @"insert into products (id, sku, name, category, brand, price, stock_on_hand, row_version, created_at, updated_at)
                             values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);";
        await using var cmd = _ds.CreateCommand(sql);
        Bind(cmd, p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Product p, CancellationToken ct)
    {
        // Manual updates bump the version so concurrent confirms see the new state.
        const string sql = @"update products
                                set sku=$2, name=$3, category=$4, brand=$5, price=$6, stock_on_hand=$7,
                                    row_version=$8 + 1, updated_at=$10
                              where id=$1;";
        await using var cmd = _ds.CreateCommand(sql);
        Bind(cmd, p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("delete from products where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(SelectAll + " where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(SelectAll + " where sku = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = sku });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return Array.Empty<Product>();
        await using var cmd = _ds.CreateCommand(SelectAll + " where id = ANY($1);");
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Product>();
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<IReadOnlyList<Product>> ListAsync(ProductCategory? category, bool? lowStock, int skip, int take, CancellationToken ct)
    {
        var (where, ps) = BuildWhere(category, lowStock);
        var sql = SelectAll + where + $" order by name asc offset {skip} limit {take};";
        await using var cmd = _ds.CreateCommand(sql);
        foreach (var p in ps) cmd.Parameters.Add(p);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Product>();
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<int> CountAsync(ProductCategory? category, bool? lowStock, CancellationToken ct)
    {
        var (where, ps) = BuildWhere(category, lowStock);
        var sql = "select count(*) from products" + where;
        await using var cmd = _ds.CreateCommand(sql);
        foreach (var p in ps) cmd.Parameters.Add(p);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<bool> TryDecrementStockAsync(Guid productId, int qty, int expectedRowVersion, CancellationToken ct)
    {
        // Conditional update: only succeeds if the row hasn't changed since we read it.
        const string sql = @"update products
                                set stock_on_hand = stock_on_hand - $2,
                                    row_version   = row_version + 1,
                                    updated_at    = now()
                              where id = $1 and row_version = $3 and stock_on_hand >= $2;";
        await using var cmd = _ds.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter { Value = productId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = qty });
        cmd.Parameters.Add(new NpgsqlParameter { Value = expectedRowVersion });
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    private const string SelectAll = "select id, sku, name, category, brand, price, stock_on_hand, row_version, created_at, updated_at from products";

    private static (string where, List<NpgsqlParameter> ps) BuildWhere(ProductCategory? category, bool? lowStock)
    {
        var ps = new List<NpgsqlParameter>();
        var clauses = new List<string>();
        if (category is { } c)
        {
            ps.Add(new NpgsqlParameter { Value = (short)c });
            clauses.Add($"category = ${ps.Count}");
        }
        if (lowStock == true) clauses.Add($"stock_on_hand <= {LowStockThreshold}");
        return clauses.Count == 0 ? ("", ps) : (" where " + string.Join(" and ", clauses), ps);
    }

    private static void Bind(NpgsqlCommand cmd, Product p)
    {
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.Id });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.Sku });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.Name });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (short)p.Category });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.Brand });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.Price });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.StockOnHand });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.RowVersion });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.CreatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter { Value = p.UpdatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
    }

    private static Product Map(NpgsqlDataReader r) => Product.Hydrate(
        r.GetGuid(0), r.GetString(1), r.GetString(2),
        (ProductCategory)r.GetInt16(3),
        r.GetString(4), r.GetDecimal(5), r.GetInt32(6), r.GetInt32(7),
        r.GetDateTime(8), r.GetDateTime(9));
}

using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using Npgsql;
using NpgsqlTypes;

namespace BallastlaneTechStore.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly NpgsqlDataSource _ds;
    public CustomerRepository(NpgsqlDataSource ds) => _ds = ds;

    public async Task AddAsync(Customer c, CancellationToken ct)
    {
        const string sql = @"insert into customers (id, company, contact_name, email, phone, status, owner_id, created_at, updated_at)
                             values ($1,$2,$3,$4,$5,$6,$7,$8,$9);";
        await using var cmd = _ds.CreateCommand(sql);
        Bind(cmd, c);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Customer c, CancellationToken ct)
    {
        const string sql = @"update customers set company=$2, contact_name=$3, email=$4, phone=$5, status=$6, updated_at=$9 where id=$1;";
        await using var cmd = _ds.CreateCommand(sql);
        Bind(cmd, c);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("delete from customers where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id, company, contact_name, email, phone, status, owner_id, created_at, updated_at from customers where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<IReadOnlyList<Customer>> ListAsync(CustomerStatus? status, int skip, int take, CancellationToken ct)
    {
        var sql = "select id, company, contact_name, email, phone, status, owner_id, created_at, updated_at from customers"
                  + (status is null ? "" : " where status = $1")
                  + $" order by created_at desc offset {skip} limit {take};";
        await using var cmd = _ds.CreateCommand(sql);
        if (status is { } s) cmd.Parameters.Add(new NpgsqlParameter { Value = (short)s });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Customer>();
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    public async Task<int> CountAsync(CustomerStatus? status, CancellationToken ct)
    {
        var sql = "select count(*) from customers" + (status is null ? "" : " where status = $1");
        await using var cmd = _ds.CreateCommand(sql);
        if (status is { } s) cmd.Parameters.Add(new NpgsqlParameter { Value = (short)s });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static void Bind(NpgsqlCommand cmd, Customer c)
    {
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.Id });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.Company });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.ContactName });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.Email });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)c.Phone ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (short)c.Status });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.OwnerId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.CreatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        cmd.Parameters.Add(new NpgsqlParameter { Value = c.UpdatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
    }

    private static Customer Map(NpgsqlDataReader r) => Customer.Hydrate(
        r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
        r.IsDBNull(4) ? null : r.GetString(4),
        (CustomerStatus)r.GetInt16(5),
        r.GetGuid(6), r.GetDateTime(7), r.GetDateTime(8));
}

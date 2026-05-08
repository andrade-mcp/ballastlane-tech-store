using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using Npgsql;
using NpgsqlTypes;

namespace BallastlaneTechStore.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly NpgsqlDataSource _ds;
    public UserRepository(NpgsqlDataSource ds) => _ds = ds;

    public async Task AddAsync(User u, CancellationToken ct)
    {
        const string sql = "insert into users (id, email, password_hash, display_name, role, created_at) values ($1,$2,$3,$4,$5,$6);";
        await using var cmd = _ds.CreateCommand(sql);
        cmd.Parameters.Add(new NpgsqlParameter { Value = u.Id });
        cmd.Parameters.Add(new NpgsqlParameter { Value = u.Email });
        cmd.Parameters.Add(new NpgsqlParameter { Value = u.PasswordHash });
        cmd.Parameters.Add(new NpgsqlParameter { Value = u.DisplayName });
        cmd.Parameters.Add(new NpgsqlParameter { Value = (short)u.Role });
        cmd.Parameters.Add(new NpgsqlParameter { Value = u.CreatedAt, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id, email, password_hash, display_name, role, created_at from users where id = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    public async Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id, email, password_hash, display_name, role, created_at from users where email = $1;");
        cmd.Parameters.Add(new NpgsqlParameter { Value = normalisedEmail });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    private static User Map(NpgsqlDataReader r) => User.Hydrate(
        r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
        (UserRole)r.GetInt16(4), r.GetDateTime(5));
}

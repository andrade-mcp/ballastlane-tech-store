using System.Reflection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BallastlaneTechStore.Infrastructure.Persistence;

public interface IMigrationRunner
{
    Task RunAsync(CancellationToken ct);
}

// Embedded *.sql under Persistence/Migrations is applied in lex order, tracked in
// __migrations, all inside a transaction.
public sealed class MigrationRunner : IMigrationRunner
{
    private const string MigrationResourcePrefix = "BallastlaneTechStore.Infrastructure.Persistence.Migrations.";
    private readonly NpgsqlDataSource _ds;
    private readonly ILogger<MigrationRunner> _log;

    public MigrationRunner(NpgsqlDataSource ds, ILogger<MigrationRunner> log) { _ds = ds; _log = log; }

    // Arbitrary 64-bit key — must be unique to this codebase. Any concurrent
    // MigrationRunner sharing the same Postgres serializes here.
    private const long AdvisoryLockKey = 7432894732894732L;

    public async Task RunAsync(CancellationToken ct)
    {
        // Hold a Postgres advisory lock for the whole run. Without this, two API
        // hosts cold-starting in parallel race on CREATE TABLE IF NOT EXISTS and
        // one loses with a pg_type unique-constraint violation.
        await using var lockConn = await _ds.OpenConnectionAsync(ct);
        await using (var lockCmd = lockConn.CreateCommand())
        {
            lockCmd.CommandText = "select pg_advisory_lock($1);";
            lockCmd.Parameters.Add(new NpgsqlParameter { Value = AdvisoryLockKey });
            await lockCmd.ExecuteNonQueryAsync(ct);
        }
        try
        {
            await EnsureLedgerAsync(ct);
            var applied = await LoadAppliedAsync(ct);
            var assembly = typeof(MigrationRunner).Assembly;
            var pending = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql"))
                .Select(n => (Resource: n, Name: n[MigrationResourcePrefix.Length..]))
                .Where(x => !applied.Contains(x.Name))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();

            foreach (var (resource, name) in pending)
            {
                var sql = ReadEmbedded(assembly, resource);
                await ApplyAsync(name, sql, ct);
                _log.LogInformation("Applied migration {name}", name);
            }
        }
        finally
        {
            await using var unlockCmd = lockConn.CreateCommand();
            unlockCmd.CommandText = "select pg_advisory_unlock($1);";
            unlockCmd.Parameters.Add(new NpgsqlParameter { Value = AdvisoryLockKey });
            try { await unlockCmd.ExecuteNonQueryAsync(ct); } catch { /* lock auto-releases on close */ }
        }
    }

    private async Task EnsureLedgerAsync(CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "create table if not exists __migrations (name text primary key, applied_at timestamptz not null default now());";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<HashSet<string>> LoadAppliedAsync(CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select name from __migrations;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var set = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct)) set.Add(reader.GetString(0));
        return set;
    }

    private async Task ApplyAsync(string name, string sql, CancellationToken ct)
    {
        await using var conn = await _ds.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var record = conn.CreateCommand())
        {
            record.Transaction = tx;
            record.CommandText = "insert into __migrations(name) values ($1);";
            record.Parameters.Add(new NpgsqlParameter { Value = name });
            await record.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private static string ReadEmbedded(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

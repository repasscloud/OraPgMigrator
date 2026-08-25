using Npgsql;

namespace OraPgMigrator.Postgres.Validation;

/// <summary>Row counter for the migration target, used by `orapg validate` (spec §36).</summary>
public sealed class PostgresRowCounter
{
    private readonly string _connectionString;

    public PostgresRowCounter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> CountAsync(string schema, string table, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{schema}\".\"{table}\"";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return System.Convert.ToInt64(result);
    }
}

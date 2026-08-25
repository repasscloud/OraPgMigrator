using OraPgMigrator.Oracle.Connection;
using OracleClient = global::Oracle.ManagedDataAccess.Client;

namespace OraPgMigrator.Oracle.SchemaReader;

/// <summary>Standalone exact row counter, used by `orapg validate` without a full schema scan.</summary>
public sealed class OracleRowCounter
{
    private readonly OracleConnectionSettings _settings;

    public OracleRowCounter(OracleConnectionSettings settings)
    {
        _settings = settings;
    }

    public async Task<long> CountAsync(string schema, string table, CancellationToken cancellationToken)
    {
        var owner = OracleIdentifiers.Normalize(schema);
        var tableName = OracleIdentifiers.Normalize(table);

        await using var connection = new OracleClient.OracleConnection(OracleConnectionStringFactory.Build(_settings));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{owner}\".\"{tableName}\"";
        command.CommandTimeout = 0;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return System.Convert.ToInt64(result);
    }
}

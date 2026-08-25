using System.Data;
using System.Runtime.CompilerServices;
using OraPgMigrator.Core.Abstractions;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Oracle.Connection;
using OracleClient = global::Oracle.ManagedDataAccess.Client;
using OracleTypes = global::Oracle.ManagedDataAccess.Types;

namespace OraPgMigrator.Oracle.DataReader;

/// <summary>
/// Streams table rows from Oracle using <see cref="CommandBehavior.SequentialAccess"/>
/// so LOB columns are read forward-only without buffering the whole row set. Each
/// call opens its own connection so concurrent exports do not share a connection.
/// </summary>
public sealed class OracleTableDataReader : ISourceTableDataReader
{
    private readonly OracleConnectionSettings _settings;

    public OracleTableDataReader(OracleConnectionSettings settings)
    {
        _settings = settings;
    }

    public async IAsyncEnumerable<object?[]> StreamRowsAsync(
        string schema,
        string table,
        IReadOnlyList<DatabaseColumn> columns,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var owner = OracleIdentifiers.Normalize(schema);
        var tableName = OracleIdentifiers.Normalize(table);
        var columnList = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));

        await using var connection = new OracleClient.OracleConnection(OracleConnectionStringFactory.Build(_settings));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {columnList} FROM \"{owner}\".\"{tableName}\"";
        command.CommandTimeout = 0;
        command.FetchSize = 512 * 1024;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        var buffer = new object?[columns.Count];
        while (await reader.ReadAsync(cancellationToken))
        {
            for (var i = 0; i < columns.Count; i++)
            {
                buffer[i] = await ReadValueAsync(reader, i, columns[i], cancellationToken);
            }

            // Yield a defensive copy: callers may hold the array across an await point.
            yield return (object?[])buffer.Clone();
        }
    }

    private static async Task<object?> ReadValueAsync(
        OracleClient.OracleDataReader reader, int ordinal, DatabaseColumn column, CancellationToken cancellationToken)
    {
        if (await reader.IsDBNullAsync(ordinal, cancellationToken))
        {
            return null;
        }

        var nativeType = column.SourceType.NativeTypeName.ToUpperInvariant();

        // CLOB/NCLOB: stream text in chunks rather than materializing OracleClob eagerly via GetValue.
        if (nativeType is "CLOB" or "NCLOB" or "LONG")
        {
            using var clob = reader.GetOracleClob(ordinal);
            return await ReadClobAsync(clob, cancellationToken);
        }

        // BLOB/RAW/LONG RAW: stream bytes in chunks.
        if (nativeType is "BLOB" or "LONG RAW")
        {
            using var blob = reader.GetOracleBlob(ordinal);
            return await ReadBlobAsync(blob, cancellationToken);
        }

        return reader.GetValue(ordinal);
    }

    private static async Task<string> ReadClobAsync(OracleTypes.OracleClob clob, CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(clob);
        return await textReader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadBlobAsync(OracleTypes.OracleBlob blob, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await blob.CopyToAsync(memoryStream, 81920, cancellationToken);
        return memoryStream.ToArray();
    }
}

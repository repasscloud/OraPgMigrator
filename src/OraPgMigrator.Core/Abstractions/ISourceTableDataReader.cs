using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Abstractions;

/// <summary>
/// Streams rows for a single table from the source database, in the exact column
/// order requested. Implementations must never buffer an entire result set in
/// memory.
/// </summary>
public interface ISourceTableDataReader
{
    IAsyncEnumerable<object?[]> StreamRowsAsync(
        string schema,
        string table,
        IReadOnlyList<DatabaseColumn> columns,
        CancellationToken cancellationToken);
}

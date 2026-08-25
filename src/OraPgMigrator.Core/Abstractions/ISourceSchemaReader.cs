using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Abstractions;

/// <summary>
/// Reads schema-level metadata (tables, columns, keys, indexes, sequences, etc.)
/// from a source database. Implemented by <c>OraPgMigrator.Oracle</c> so it can be
/// faked in unit tests without a real database.
/// </summary>
public interface ISourceSchemaReader
{
    Task<DatabaseSchema> ScanSchemaAsync(string schemaName, RowCountMode rowCountMode, CancellationToken cancellationToken);

    Task<bool> SchemaExistsAsync(string schemaName, CancellationToken cancellationToken);
}

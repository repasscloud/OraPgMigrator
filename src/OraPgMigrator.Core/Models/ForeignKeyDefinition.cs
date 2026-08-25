namespace OraPgMigrator.Core.Models;

public sealed record ForeignKeyDefinition(
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns);

namespace OraPgMigrator.Core.Models;

public sealed record PrimaryKeyDefinition(
    string Name,
    IReadOnlyList<string> Columns);

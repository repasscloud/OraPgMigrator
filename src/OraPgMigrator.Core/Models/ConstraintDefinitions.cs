namespace OraPgMigrator.Core.Models;

public sealed record UniqueConstraintDefinition(
    string Name,
    IReadOnlyList<string> Columns);

public sealed record CheckConstraintDefinition(
    string Name,
    string Expression);

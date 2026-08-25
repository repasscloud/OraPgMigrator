namespace OraPgMigrator.Core.Models;

public sealed record DatabaseTable(
    string Schema,
    string Name,
    long RowCount,
    IReadOnlyList<DatabaseColumn> Columns,
    PrimaryKeyDefinition? PrimaryKey,
    IReadOnlyList<ForeignKeyDefinition> ForeignKeys,
    IReadOnlyList<UniqueConstraintDefinition> UniqueConstraints,
    IReadOnlyList<CheckConstraintDefinition> CheckConstraints,
    IReadOnlyList<IndexDefinition> Indexes,
    IReadOnlyList<TriggerDefinition> Triggers,
    PartitioningInfo? Partitioning,
    string? Comment);

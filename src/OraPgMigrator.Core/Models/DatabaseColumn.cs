namespace OraPgMigrator.Core.Models;

public sealed record DatabaseColumn(
    string Name,
    int OrdinalPosition,
    SourceDataType SourceType,
    bool Nullable,
    ColumnDefault? Default,
    string? Comment);

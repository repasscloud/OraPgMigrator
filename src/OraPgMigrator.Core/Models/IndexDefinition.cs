namespace OraPgMigrator.Core.Models;

public enum IndexKind
{
    Normal,
    Bitmap,
    FunctionBased,
    Reverse,
    Domain,
    Partitioned
}

public sealed record IndexDefinition(
    string Name,
    IReadOnlyList<string> Columns,
    bool IsUnique,
    IndexKind Kind);

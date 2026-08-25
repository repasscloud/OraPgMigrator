using System.Text.Json.Serialization;
using OraPgMigrator.Core.Json;

namespace OraPgMigrator.Core.Models;

[JsonConverter(typeof(CamelCaseStringEnumConverter<IndexKind>))]
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

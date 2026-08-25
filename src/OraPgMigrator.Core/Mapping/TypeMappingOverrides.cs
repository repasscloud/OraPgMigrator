namespace OraPgMigrator.Core.Mapping;

/// <summary>
/// User-supplied overrides (e.g. from a --type-map JSON file) keyed by the source
/// native type name (case-insensitive), always taking precedence over built-in
/// mapping rules.
/// </summary>
public sealed class TypeMappingOverrides
{
    public IReadOnlyDictionary<string, string> Mappings { get; }

    public TypeMappingOverrides(IReadOnlyDictionary<string, string>? mappings = null)
    {
        Mappings = new Dictionary<string, string>(
            mappings ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static TypeMappingOverrides Empty { get; } = new();

    public bool TryGet(string nativeTypeName, out string targetType) =>
        Mappings.TryGetValue(nativeTypeName, out targetType!);
}

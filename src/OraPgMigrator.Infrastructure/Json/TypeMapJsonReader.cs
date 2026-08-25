using System.Text.Json;
using System.Text.Json.Serialization;
using OraPgMigrator.Core.Mapping;

namespace OraPgMigrator.Infrastructure.Json;

/// <summary>Reads a --type-map JSON file of the form <c>{ "typeMappings": { "XMLTYPE": "xml" } }</c>.</summary>
public static class TypeMapJsonReader
{
    internal sealed class TypeMapDocument
    {
        [JsonPropertyName("typeMappings")]
        public Dictionary<string, string> TypeMappings { get; set; } = new();
    }

    public static async Task<TypeMappingOverrides> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.TypeMapDocument, cancellationToken);
        return new TypeMappingOverrides(doc?.TypeMappings);
    }
}

using System.Text.Json.Serialization;
using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Infrastructure.Json;

/// <summary>
/// Source-generated System.Text.Json metadata for camelCase documents (metadata.json,
/// --type-map files), so publish is fully AOT/trim-safe with no reflection-based fallback.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DatabaseSchema))]
[JsonSerializable(typeof(TypeMapJsonReader.TypeMapDocument))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}

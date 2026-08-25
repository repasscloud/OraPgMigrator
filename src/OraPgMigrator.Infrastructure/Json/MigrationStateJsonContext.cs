using System.Text.Json.Serialization;
using OraPgMigrator.Core.Migration;

namespace OraPgMigrator.Infrastructure.Json;

/// <summary>
/// Source-generated System.Text.Json metadata for migration-state.json. Uses default
/// (Pascal-case) property naming to match the format written before AOT source
/// generation was introduced, so existing state files remain readable.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MigrationState))]
internal sealed partial class MigrationStateJsonContext : JsonSerializerContext
{
}

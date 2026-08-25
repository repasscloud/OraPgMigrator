using System.Text.Json;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Infrastructure.FileSystem;

namespace OraPgMigrator.Infrastructure.Json;

/// <summary>
/// Serializes/deserializes metadata.json — the full scanned-schema snapshot that
/// later stages (ddl, export) read instead of reconnecting to the source purely
/// for metadata (spec §9). Output is deterministic: camelCase properties, stable
/// field order (record declaration order), indented for human readability.
/// Never contains connection passwords.
/// </summary>
public static class MetadataJsonSerializer
{
    public static async Task WriteAsync(string path, DatabaseSchema schema, CancellationToken cancellationToken)
    {
        await AtomicFile.WriteAsync(path, async (stream, ct) =>
        {
            await JsonSerializer.SerializeAsync(stream, schema, AppJsonContext.Default.DatabaseSchema, ct);
        }, cancellationToken);
    }

    public static async Task<DatabaseSchema> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var schema = await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.DatabaseSchema, cancellationToken);
        return schema ?? throw new InvalidOperationException($"metadata.json at '{path}' deserialized to null.");
    }
}

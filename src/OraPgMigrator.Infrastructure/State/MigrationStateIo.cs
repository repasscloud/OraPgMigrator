using System.Text.Json;
using OraPgMigrator.Core.Migration;
using OraPgMigrator.Infrastructure.FileSystem;
using OraPgMigrator.Infrastructure.Json;

namespace OraPgMigrator.Infrastructure.State;

public static class MigrationStateIo
{
    public static async Task<MigrationState> ReadOrEmptyAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new MigrationState();
        }

        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync(stream, MigrationStateJsonContext.Default.MigrationState, cancellationToken);
        return state ?? new MigrationState();
    }

    public static async Task WriteAsync(string path, MigrationState state, CancellationToken cancellationToken)
    {
        await AtomicFile.WriteAsync(path, async (stream, ct) =>
        {
            await JsonSerializer.SerializeAsync(stream, state, MigrationStateJsonContext.Default.MigrationState, ct);
        }, cancellationToken);
    }
}

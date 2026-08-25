namespace OraPgMigrator.Infrastructure.FileSystem;

/// <summary>
/// Writes to a <c>.partial</c> file and only renames it to the final name once the
/// writer completes successfully, so an incomplete export is never mistaken for a
/// finished one (spec §2, §32). On cancellation/failure the <c>.partial</c> file is
/// left in place for diagnostics/resume rather than deleted.
/// </summary>
public static class AtomicFile
{
    public const string PartialSuffix = ".partial";

    public static string PartialPathFor(string finalPath) => finalPath + PartialSuffix;

    public static Task WriteTextAsync(string finalPath, string content, CancellationToken cancellationToken) =>
        WriteAsync(finalPath, async (stream, ct) =>
        {
            var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
            await using (writer)
            {
                await writer.WriteAsync(content.AsMemory(), ct);
                await writer.FlushAsync(ct);
            }
        }, cancellationToken);

    public static async Task WriteAsync(string finalPath, Func<Stream, CancellationToken, Task> writeAction, CancellationToken cancellationToken)
    {
        var partialPath = PartialPathFor(finalPath);
        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var stream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await writeAction(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(partialPath, finalPath);
    }
}

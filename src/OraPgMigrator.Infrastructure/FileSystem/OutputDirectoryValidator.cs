namespace OraPgMigrator.Infrastructure.FileSystem;

public sealed class OutputDirectoryException : Exception
{
    public OutputDirectoryException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Ensures a migration output directory (local or UNC) exists and is writable
/// before a long-running operation starts, surfacing a clear, actionable error
/// otherwise (spec §2, §45).
/// </summary>
public static class OutputDirectoryValidator
{
    public static void EnsureWritable(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new OutputDirectoryException($"Cannot create output directory:{Environment.NewLine}{directoryPath}{Environment.NewLine}{ex.Message}", ex);
        }

        var probeFile = Path.Combine(directoryPath, $".orapg-write-check-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(probeFile, [0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputDirectoryException($"Cannot write to output directory:{Environment.NewLine}{directoryPath}{Environment.NewLine}Access denied.", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probeFile))
                {
                    File.Delete(probeFile);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup only; a leftover probe file is not fatal.
            }
        }
    }
}

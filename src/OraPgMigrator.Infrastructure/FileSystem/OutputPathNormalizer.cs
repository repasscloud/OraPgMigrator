namespace OraPgMigrator.Infrastructure.FileSystem;

/// <summary>
/// Normalizes user-supplied output paths for Windows. Only rewrites input that is
/// unambiguously Windows-style-with-forward-slashes: UNC-like <c>//server/folder</c>
/// (rewritten to <c>\\server\folder</c>) and drive-letter paths such as
/// <c>C:/data_export</c> (rewritten to <c>C:\data_export</c>). Already-correct
/// local (<c>C:\data_export</c>) and UNC (<c>\\server\folder</c>) paths pass
/// through with separator de-duplication only. A plain POSIX-style absolute or
/// relative path (e.g. <c>/tmp/x</c>) is left untouched, since on Windows such a
/// path would never legitimately appear and rewriting it would corrupt dev/test
/// runs on non-Windows hosts.
/// </summary>
public static class OutputPathNormalizer
{
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = path.Trim();

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            // //server/folder/path -> \\server\folder\path
            trimmed = "\\\\" + trimmed[2..].Replace('/', '\\');
        }
        else if (trimmed.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // Already UNC; only fold accidental duplicate separators below.
        }
        else if (IsForwardSlashDriveLetterPath(trimmed))
        {
            // C:/data_export/sub -> C:\data_export\sub
            trimmed = trimmed.Replace('/', '\\');
        }
        else
        {
            // Not a recognizable Windows-style path (POSIX path, relative path, etc.) — leave as-is.
            return trimmed;
        }

        var isUnc = trimmed.StartsWith(@"\\", StringComparison.Ordinal);
        var body = isUnc ? trimmed[2..] : trimmed;
        while (body.Contains(@"\\", StringComparison.Ordinal))
        {
            body = body.Replace(@"\\", @"\");
        }

        return isUnc ? @"\\" + body : body;
    }

    private static bool IsForwardSlashDriveLetterPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/';

    public static bool IsUncPath(string normalizedPath) => normalizedPath.StartsWith(@"\\", StringComparison.Ordinal);
}

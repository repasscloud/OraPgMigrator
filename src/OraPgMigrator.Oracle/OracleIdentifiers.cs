namespace OraPgMigrator.Oracle;

/// <summary>
/// Oracle identifier normalization rules: unquoted identifiers are stored
/// uppercase, so an unquoted, unadorned identifier supplied on the CLI (e.g.
/// "legacy_app") should resolve the same way Oracle would resolve it.
/// </summary>
public static class OracleIdentifiers
{
    /// <summary>
    /// Normalizes an identifier the way Oracle would resolve it when unquoted.
    /// A value already wrapped in double quotes is treated as case-sensitive and
    /// returned with the quotes stripped but casing preserved.
    /// </summary>
    public static string Normalize(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var trimmed = identifier.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed.ToUpperInvariant();
    }
}

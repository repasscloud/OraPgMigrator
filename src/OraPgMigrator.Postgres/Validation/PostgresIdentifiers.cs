namespace OraPgMigrator.Postgres.Validation;

/// <summary>
/// PostgreSQL identifier policy: casing normalization, reserved-word detection,
/// length limits and quoting. Never silently resolves a collision — callers
/// surface a <see cref="OraPgMigrator.Core.Validation.ValidationIssue"/> instead.
/// </summary>
public static class PostgresIdentifiers
{
    public const int MaxLength = 63;

    // The PostgreSQL "reserved" and "reserved (can be function or type name)" keyword categories,
    // https://www.postgresql.org/docs/current/sql-keywords-appendix.html — identifiers matching one
    // of these require quoting.
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "analyse", "analyze", "and", "any", "array", "as", "asc", "asymmetric",
        "both", "case", "cast", "check", "collate", "column", "constraint", "create",
        "current_catalog", "current_date", "current_role", "current_time", "current_timestamp",
        "current_user", "default", "deferrable", "desc", "distinct", "do", "else", "end",
        "except", "false", "fetch", "for", "foreign", "from", "grant", "group", "having",
        "in", "initially", "intersect", "into", "lateral", "leading", "limit", "localtime",
        "localtimestamp", "not", "null", "offset", "on", "only", "or", "order", "placing",
        "primary", "references", "returning", "select", "session_user", "some", "symmetric",
        "table", "then", "to", "trailing", "true", "union", "unique", "user", "using",
        "variadic", "when", "where", "window", "with",
        "authorization", "binary", "collation", "concurrently", "cross", "current_schema",
        "freeze", "full", "ilike", "inner", "is", "isnull", "join", "left", "like", "natural",
        "notnull", "outer", "overlaps", "right", "similar", "tablesample", "verbose",
        "position", "group_by", "order_by"
    };

    public static bool IsReserved(string identifier) => ReservedWords.Contains(identifier);

    public static string Normalize(string identifier, IdentifierCase mode) =>
        mode == IdentifierCase.Lower ? identifier.ToLowerInvariant() : identifier;

    public static bool IsValidUnquotedIdentifier(string identifier) =>
        identifier.Length > 0
        && char.IsAsciiLetterLower(identifier[0])
        && identifier.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');

    /// <summary>Quotes an identifier only when required (reserved word, invalid unquoted form, or preserved mixed-case).</summary>
    public static string Quote(string identifier, IdentifierCase mode)
    {
        var needsQuoting = mode == IdentifierCase.Preserve
            ? !IsValidUnquotedIdentifier(identifier)
            : IsReserved(identifier) || !IsValidUnquotedIdentifier(identifier);

        return needsQuoting ? $"\"{identifier.Replace("\"", "\"\"")}\"" : identifier;
    }
}

namespace OraPgMigrator.Core.Models;

public enum DefaultKind
{
    None,
    Literal,
    CurrentTimestamp,
    SequenceNextVal,
    Unrecognized
}

/// <summary>
/// A column default as captured from the source, plus a best-effort classification
/// used later to decide whether it can be safely translated to PostgreSQL.
/// </summary>
public sealed record ColumnDefault(
    string RawExpression,
    DefaultKind Kind,
    string? SequenceName = null);

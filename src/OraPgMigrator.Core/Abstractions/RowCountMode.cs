namespace OraPgMigrator.Core.Abstractions;

public enum RowCountMode
{
    /// <summary>SELECT COUNT(*) per table. Authoritative but slower.</summary>
    Exact,

    /// <summary>Source optimizer statistics. Fast but potentially stale.</summary>
    Statistics
}

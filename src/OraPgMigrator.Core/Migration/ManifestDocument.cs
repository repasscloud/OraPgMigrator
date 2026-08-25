namespace OraPgMigrator.Core.Migration;

public static class ManifestColumns
{
    public const string Include = "Include";
    public const string SourceSchema = "SourceSchema";
    public const string SourceTable = "SourceTable";
    public const string RowCount = "RowCount";
    public const string GenerateDDL = "GenerateDDL";
    public const string ExportData = "ExportData";
    public const string TargetSchema = "TargetSchema";
    public const string TargetTable = "TargetTable";

    public static readonly IReadOnlyList<string> Required =
    [
        Include, SourceSchema, SourceTable, RowCount, GenerateDDL, ExportData, TargetSchema, TargetTable
    ];
}

/// <summary>Thrown when a manifest.csv is missing required columns or contains malformed rows.</summary>
public sealed class ManifestValidationException : Exception
{
    public ManifestValidationException(string message) : base(message)
    {
    }
}

/// <summary>
/// The full set of manifest rows for a migration run. Row order is not
/// significant; commands reading a manifest must tolerate reordering and must not
/// depend on scan-time ordering.
/// </summary>
public sealed class ManifestDocument
{
    public IReadOnlyList<ManifestEntry> Entries { get; }

    public ManifestDocument(IReadOnlyList<ManifestEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>Entries selected for the given stage, keyed by SourceSchema.SourceTable (upper-cased).</summary>
    public IEnumerable<ManifestEntry> ForDdl() => Entries.Where(e => e.Include && e.GenerateDdl);

    public IEnumerable<ManifestEntry> ForExport() => Entries.Where(e => e.Include && e.ExportData);

    public static void ValidateHeader(IReadOnlyList<string> header)
    {
        var missing = ManifestColumns.Required.Where(c => !header.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw new ManifestValidationException(
                $"manifest.csv is missing required column(s): {string.Join(", ", missing)}");
        }
    }
}

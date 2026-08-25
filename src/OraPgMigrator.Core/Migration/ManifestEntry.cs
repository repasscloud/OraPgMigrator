namespace OraPgMigrator.Core.Migration;

/// <summary>One editable row of manifest.csv.</summary>
public sealed record ManifestEntry(
    bool Include,
    string SourceSchema,
    string SourceTable,
    long RowCount,
    bool GenerateDdl,
    bool ExportData,
    string TargetSchema,
    string TargetTable);

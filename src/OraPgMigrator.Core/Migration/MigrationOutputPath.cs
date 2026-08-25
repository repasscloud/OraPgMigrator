namespace OraPgMigrator.Core.Migration;

/// <summary>
/// Central place that knows the on-disk layout of a migration output directory.
/// Always uses <see cref="Path.Combine"/> so local and UNC roots both work.
/// </summary>
public sealed record MigrationOutputPath(string RootPath)
{
    public string ManifestPath => Path.Combine(RootPath, "manifest.csv");
    public string MetadataPath => Path.Combine(RootPath, "metadata.json");
    public string ReportPath => Path.Combine(RootPath, "report.txt");
    public string MigrationStatePath => Path.Combine(RootPath, "migration-state.json");

    public string DdlDirectory => Path.Combine(RootPath, "ddl");
    public string DdlSchemasDirectory => Path.Combine(DdlDirectory, "schemas");
    public string DdlTablesDirectory => Path.Combine(DdlDirectory, "tables");
    public string DdlPrimaryKeysDirectory => Path.Combine(DdlDirectory, "primary-keys");
    public string DdlSequencesDirectory => Path.Combine(DdlDirectory, "sequences");
    public string DdlForeignKeysDirectory => Path.Combine(DdlDirectory, "foreign-keys");
    public string DdlIndexesDirectory => Path.Combine(DdlDirectory, "indexes");

    public string DataDirectory => Path.Combine(RootPath, "data");
    public string LoadDirectory => Path.Combine(RootPath, "load");
    public string LogsDirectory => Path.Combine(RootPath, "logs");
}

using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Loader;

/// <summary>
/// Generates a `\copy` script for one exported CSV, and a master load-all.sql
/// that concatenates them in the caller-supplied order (typically FK-dependency
/// order from <c>TableDependencyGraph</c>). CSV dialect must match
/// <c>OraPgMigrator.Infrastructure.Csv.MigrationCsvWriter</c> exactly (comma
/// delimiter, header row, "\N" NULL token).
/// </summary>
public sealed class CopyScriptGenerator
{
    private readonly IdentifierCase _identifierCase;

    public CopyScriptGenerator(IdentifierCase identifierCase)
    {
        _identifierCase = identifierCase;
    }

    public string GenerateForTable(string targetSchema, string targetTable, string relativeCsvPath)
    {
        var schema = PostgresIdentifiers.Quote(PostgresIdentifiers.Normalize(targetSchema, _identifierCase), _identifierCase);
        var table = PostgresIdentifiers.Quote(PostgresIdentifiers.Normalize(targetTable, _identifierCase), _identifierCase);
        var csvPath = relativeCsvPath.Replace('\\', '/');

        return $"""
            \copy {schema}.{table} FROM '{csvPath}' WITH (FORMAT csv, HEADER true, NULL '\N');

            """;
    }

    public string GenerateLoadAll(IReadOnlyList<string> orderedLoadScriptFileNames)
    {
        var lines = orderedLoadScriptFileNames.Select(f => $"\\i {f.Replace('\\', '/')}");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}

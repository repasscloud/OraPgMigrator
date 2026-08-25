using System.CommandLine;
using Microsoft.Extensions.Logging;
using OraPgMigrator.Core.Abstractions;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Cli.Options;

public static class CommonOptions
{
    public static Option<string> Output() => new("--output") { Description = "Migration output directory. Accepts local paths (C:\\data_export) and UNC paths (\\\\server\\share).", Required = true };

    public static Option<string> Manifest() => new("--manifest") { Description = "Path to manifest.csv.", Required = true };

    public static Option<string> Metadata() => new("--metadata") { Description = "Path to metadata.json.", Required = true };

    public static Option<string?> TypeMap() => new("--type-map") { Description = "Path to a JSON file of custom Oracle -> PostgreSQL type overrides." };

    public static Option<RowCountMode> RowCountMode() => new("--row-count-mode")
    {
        Description = "exact = SELECT COUNT(*) per table (authoritative). statistics = Oracle optimizer stats (fast, potentially stale).",
        DefaultValueFactory = _ => Core.Abstractions.RowCountMode.Exact
    };

    public static Option<IdentifierCase> IdentifierCase() => new("--identifier-case")
    {
        Description = "lower = normalize identifiers to lowercase (default). preserve = keep source casing (quoted where needed).",
        DefaultValueFactory = _ => Postgres.Validation.IdentifierCase.Lower
    };

    public static Option<LogLevel> LogLevel() => new("--log-level")
    {
        Description = "Minimum log level written to console/log file.",
        DefaultValueFactory = _ => Microsoft.Extensions.Logging.LogLevel.Information
    };

    public static Option<string?> LogFile() => new("--log-file") { Description = "Optional path to a detailed log file." };

    public static Option<int> Parallel() => new("--parallel")
    {
        Description = "Maximum number of tables exported concurrently.",
        DefaultValueFactory = _ => 1
    };

    public static Option<bool> Resume() => new("--resume") { Description = "Skip tables already recorded complete in migration-state.json." };

    public static Option<string?> TargetSchemaOverride() => new("--target-schema") { Description = "Default PostgreSQL target schema for manifest rows (default: public)." };
}

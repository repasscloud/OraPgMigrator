using System.CommandLine;
using OraPgMigrator.Cli.Options;
using OraPgMigrator.Core.Migration;

namespace OraPgMigrator.Cli.Commands;

/// <summary>
/// `orapg migrate`: convenience orchestrator that runs scan, ddl, and export in
/// sequence against one output root, re-using the exact same argument parsing and
/// business logic as the standalone commands (spec §7 "Full Migration"). Load and
/// validate stages are not orchestrated yet — this command never claims to run
/// them.
/// </summary>
public static class MigrateCommand
{
    public static Command Build()
    {
        var oracleOptions = new OracleConnectionOptionSet();
        var output = CommonOptions.Output();
        var rowCountMode = CommonOptions.RowCountMode();
        var identifierCase = CommonOptions.IdentifierCase();
        var targetSchema = CommonOptions.TargetSchemaOverride();
        var parallel = CommonOptions.Parallel();
        var logLevel = CommonOptions.LogLevel();
        var logFile = CommonOptions.LogFile();
        var dryRun = new Option<bool>("--dry-run") { Description = "Scan and report only; do not generate DDL or export data." };

        var command = new Command("migrate", "Convenience command: runs scan, then ddl, then export against one output directory.");
        oracleOptions.AddTo(command);
        command.Options.Add(output);
        command.Options.Add(rowCountMode);
        command.Options.Add(identifierCase);
        command.Options.Add(targetSchema);
        command.Options.Add(parallel);
        command.Options.Add(logLevel);
        command.Options.Add(logFile);
        command.Options.Add(dryRun);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var outputRoot = parseResult.GetRequiredValue(output);
            var paths = new MigrationOutputPath(outputRoot);
            var oracleArgs = BuildOracleArgs(parseResult, oracleOptions);
            var logArgs = BuildLogArgs(parseResult, logLevel, logFile);

            Console.WriteLine("=== Step 1/3: scan ===");
            var scanArgs = new List<string> { "scan" };
            scanArgs.AddRange(oracleArgs);
            scanArgs.AddRange(["--output", outputRoot]);
            scanArgs.AddRange(["--row-count-mode", parseResult.GetValue(rowCountMode).ToString()!]);
            scanArgs.AddRange(["--identifier-case", parseResult.GetValue(identifierCase).ToString()!]);
            if (parseResult.GetValue(targetSchema) is { } ts)
            {
                scanArgs.AddRange(["--target-schema", ts]);
            }
            scanArgs.AddRange(logArgs);

            var scanExit = await ScanCommand.Build().Parse(scanArgs).InvokeAsync(cancellationToken: cancellationToken);
            if (scanExit != ExitCodes.Success || parseResult.GetValue(dryRun))
            {
                return scanExit;
            }

            Console.WriteLine("=== Step 2/3: ddl ===");
            var ddlArgs = new List<string>
            {
                "ddl",
                "--manifest", paths.ManifestPath,
                "--metadata", paths.MetadataPath,
                "--output", paths.DdlDirectory,
                "--identifier-case", parseResult.GetValue(identifierCase).ToString()!
            };
            ddlArgs.AddRange(logArgs);
            var ddlExit = await DdlCommand.Build().Parse(ddlArgs).InvokeAsync(cancellationToken: cancellationToken);
            if (ddlExit is not (ExitCodes.Success or ExitCodes.UnsupportedSchemaOrData))
            {
                return ddlExit;
            }

            Console.WriteLine("=== Step 3/3: export ===");
            var exportArgs = new List<string> { "export" };
            exportArgs.AddRange(oracleArgs);
            exportArgs.AddRange([
                "--manifest", paths.ManifestPath,
                "--metadata", paths.MetadataPath,
                "--output", paths.DataDirectory,
                "--parallel", parseResult.GetValue(parallel).ToString()!,
                "--identifier-case", parseResult.GetValue(identifierCase).ToString()!
            ]);
            exportArgs.AddRange(logArgs);
            var exportExit = await ExportCommand.Build().Parse(exportArgs).InvokeAsync(cancellationToken: cancellationToken);

            return exportExit != ExitCodes.Success ? exportExit : ddlExit;
        });

        return command;
    }

    private static List<string> BuildOracleArgs(ParseResult parseResult, OracleConnectionOptionSet oracleOptions)
    {
        var args = new List<string>();
        AddIfSet(args, "--oracle-host", parseResult.GetValue(oracleOptions.Host));
        AddIfSet(args, "--oracle-host-env", parseResult.GetValue(oracleOptions.HostEnv));
        AddIfSet(args, "--oracle-port", parseResult.GetValue(oracleOptions.Port)?.ToString());
        AddIfSet(args, "--oracle-port-env", parseResult.GetValue(oracleOptions.PortEnv));
        AddIfSet(args, "--oracle-sid", parseResult.GetValue(oracleOptions.Sid));
        AddIfSet(args, "--oracle-sid-env", parseResult.GetValue(oracleOptions.SidEnv));
        AddIfSet(args, "--oracle-service-name", parseResult.GetValue(oracleOptions.ServiceName));
        AddIfSet(args, "--oracle-service-name-env", parseResult.GetValue(oracleOptions.ServiceNameEnv));
        AddIfSet(args, "--oracle-user", parseResult.GetValue(oracleOptions.User));
        AddIfSet(args, "--oracle-user-env", parseResult.GetValue(oracleOptions.UserEnv));
        AddIfSet(args, "--oracle-password", parseResult.GetValue(oracleOptions.Password));
        AddIfSet(args, "--oracle-password-env", parseResult.GetValue(oracleOptions.PasswordEnv));
        AddIfSet(args, "--schema", parseResult.GetValue(oracleOptions.Schema));
        AddIfSet(args, "--schema-env", parseResult.GetValue(oracleOptions.SchemaEnv));
        return args;
    }

    private static List<string> BuildLogArgs(ParseResult parseResult, Option<Microsoft.Extensions.Logging.LogLevel> logLevel, Option<string?> logFile)
    {
        var args = new List<string> { "--log-level", parseResult.GetValue(logLevel).ToString()! };
        AddIfSet(args, "--log-file", parseResult.GetValue(logFile));
        return args;
    }

    private static void AddIfSet(List<string> args, string flag, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            args.Add(flag);
            args.Add(value);
        }
    }
}

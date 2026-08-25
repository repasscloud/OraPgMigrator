using System.CommandLine;
using Microsoft.Extensions.Logging;
using OraPgMigrator.Cli.Options;
using OraPgMigrator.Core.Migration;
using OraPgMigrator.Infrastructure;
using OraPgMigrator.Infrastructure.Csv;
using OraPgMigrator.Infrastructure.FileSystem;
using OraPgMigrator.Infrastructure.Json;
using OraPgMigrator.Oracle.SchemaReader;
using OraPgMigrator.Postgres.Ddl;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Cli.Commands;

/// <summary>
/// `orapg scan`: connects to Oracle, inspects the selected schema, and produces
/// manifest.csv, metadata.json and report.txt (spec §10, §58 items 1-9).
/// </summary>
public static class ScanCommand
{
    public static Command Build()
    {
        var oracleOptions = new OracleConnectionOptionSet();
        var output = CommonOptions.Output();
        var rowCountMode = CommonOptions.RowCountMode();
        var identifierCase = CommonOptions.IdentifierCase();
        var targetSchema = CommonOptions.TargetSchemaOverride();
        var logLevel = CommonOptions.LogLevel();
        var logFile = CommonOptions.LogFile();

        var command = new Command("scan", "Connect to Oracle, scan a schema, and produce manifest.csv, metadata.json and report.txt.");
        oracleOptions.AddTo(command);
        command.Options.Add(output);
        command.Options.Add(rowCountMode);
        command.Options.Add(identifierCase);
        command.Options.Add(targetSchema);
        command.Options.Add(logLevel);
        command.Options.Add(logFile);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var logger = CliHost.CreateLogger(parseResult, logLevel, logFile);
            var envResolver = new EnvironmentVariableResolver();

            try
            {
                var settings = oracleOptions.Resolve(parseResult, envResolver);
                var outputRoot = OutputPathNormalizer.Normalize(parseResult.GetRequiredValue(output));
                var mode = parseResult.GetValue(rowCountMode);
                var idCase = parseResult.GetValue(identifierCase);
                var targetSchemaName = parseResult.GetValue(targetSchema) ?? "public";

                OutputDirectoryValidator.EnsureWritable(outputRoot);
                var paths = new MigrationOutputPath(outputRoot);

                logger.LogInformation("Connecting to Oracle {Host}:{Port}", settings.Host, settings.Port);
                var reader = new OracleSchemaReader(settings);

                if (!await reader.SchemaExistsAsync(settings.ResolvedSchema, cancellationToken))
                {
                    Console.Error.WriteLine($"Schema '{settings.ResolvedSchema}' was not found or cannot be read by user '{settings.Username}'.");
                    return ExitCodes.SchemaAccessFailure;
                }

                Console.WriteLine($"Selected schema: {settings.ResolvedSchema}");
                logger.LogInformation("Scanning schema {Schema}", settings.ResolvedSchema);

                var schema = await reader.ScanSchemaAsync(settings.ResolvedSchema, mode, cancellationToken);

                Console.WriteLine($"Found {schema.Tables.Count} tables, {schema.Tables.Sum(t => t.RowCount):N0} rows.");

                var typeMapper = new OracleToPostgresTypeMapper();
                var issues = SchemaAnalyzer.Analyze(schema, typeMapper, idCase);

                var manifestEntries = schema.Tables
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .Select(t => new ManifestEntry(
                        Include: true,
                        SourceSchema: schema.SchemaName,
                        SourceTable: t.Name,
                        RowCount: t.RowCount,
                        GenerateDdl: true,
                        ExportData: true,
                        TargetSchema: targetSchemaName,
                        TargetTable: PostgresIdentifiers.Normalize(t.Name, idCase)))
                    .ToList();

                await ManifestCsvIo.WriteAsync(paths.ManifestPath, manifestEntries, cancellationToken);
                await MetadataJsonSerializer.WriteAsync(paths.MetadataPath, schema, cancellationToken);
                var report = ScanReportBuilder.Build(schema, issues);
                await AtomicFile.WriteTextAsync(paths.ReportPath, report, cancellationToken);

                Console.WriteLine($"Wrote: {paths.ManifestPath}");
                Console.WriteLine($"Wrote: {paths.MetadataPath}");
                Console.WriteLine($"Wrote: {paths.ReportPath}");

                return ExitCodes.Success;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.InvalidArguments;
            }
            catch (OutputDirectoryException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.OutputStorageFailure;
            }
            catch (Exception ex) when (IsOracleConnectionFailure(ex))
            {
                Console.Error.WriteLine($"Unable to connect to Oracle: {ex.Message}");
                return ExitCodes.OracleConnectionFailure;
            }
        });

        return command;
    }

    internal static bool IsOracleConnectionFailure(Exception ex) =>
        ex.GetType().FullName == "Oracle.ManagedDataAccess.Client.OracleException";
}

using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OraPgMigrator.Cli.Options;
using OraPgMigrator.Core.Conversion;
using OraPgMigrator.Core.Migration;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Infrastructure.Csv;
using OraPgMigrator.Infrastructure.FileSystem;
using OraPgMigrator.Infrastructure.Json;
using OraPgMigrator.Infrastructure.State;
using OraPgMigrator.Oracle.Conversion;
using OraPgMigrator.Oracle.DataReader;
using OraPgMigrator.Postgres.Loader;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Cli.Commands;

/// <summary>
/// `orapg export`: streams each selected table's data from Oracle to a CSV file
/// under bounded parallelism (spec §23-§27, §31). Never buffers a whole table in
/// memory; each concurrent export uses its own Oracle connection.
/// </summary>
public static class ExportCommand
{
    public static Command Build()
    {
        var oracleOptions = new OracleConnectionOptionSet();
        var manifest = CommonOptions.Manifest();
        var metadata = CommonOptions.Metadata();
        var output = CommonOptions.Output();
        var parallel = CommonOptions.Parallel();
        var resume = CommonOptions.Resume();
        var identifierCase = CommonOptions.IdentifierCase();
        var logLevel = CommonOptions.LogLevel();
        var logFile = CommonOptions.LogFile();

        var command = new Command("export", "Stream selected table data from Oracle to per-table CSV files.");
        oracleOptions.AddTo(command);
        command.Options.Add(manifest);
        command.Options.Add(metadata);
        command.Options.Add(output);
        command.Options.Add(parallel);
        command.Options.Add(resume);
        command.Options.Add(identifierCase);
        command.Options.Add(logLevel);
        command.Options.Add(logFile);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var logger = CliHost.CreateLogger(parseResult, logLevel, logFile);
            var envResolver = new Infrastructure.EnvironmentVariableResolver();

            try
            {
                var settings = oracleOptions.Resolve(parseResult, envResolver);
                var idCase = parseResult.GetValue(identifierCase);
                var outputRoot = OutputPathNormalizer.Normalize(parseResult.GetRequiredValue(output));
                var maxParallel = Math.Max(1, parseResult.GetValue(parallel));
                var useResume = parseResult.GetValue(resume);

                OutputDirectoryValidator.EnsureWritable(outputRoot);

                var manifestDoc = ManifestCsvIo.Read(parseResult.GetRequiredValue(manifest));
                var schema = await MetadataJsonSerializer.ReadAsync(parseResult.GetRequiredValue(metadata), cancellationToken);
                var tablesBySourceKey = schema.Tables.ToDictionary(t => $"{t.Schema}.{t.Name}", StringComparer.OrdinalIgnoreCase);

                var migrationRoot = Path.GetDirectoryName(outputRoot) is { Length: > 0 } parent ? parent : outputRoot;
                var statePath = Path.Combine(migrationRoot, "migration-state.json");
                var state = await MigrationStateIo.ReadOrEmptyAsync(statePath, cancellationToken);
                var loadDirectory = Path.Combine(migrationRoot, "load");

                var entries = manifestDoc.ForExport().ToList();
                var dataReader = new OracleTableDataReader(settings);
                var converter = new OracleValueConverter();
                var csvWriter = new MigrationCsvWriter();
                var copyScriptGenerator = new CopyScriptGenerator(idCase);

                using var throttle = new SemaphoreSlim(maxParallel);
                var stateLock = new object();
                var results = new List<(string Key, bool Success, long Rows, string? Error)>();
                var resultsLock = new object();

                var tasks = entries.Select(async entry =>
                {
                    var key = $"{entry.SourceSchema}.{entry.SourceTable}";

                    if (useResume && state.IsComplete(key))
                    {
                        logger.LogInformation("Skipping {Table}: already complete (--resume).", key);
                        return;
                    }

                    if (!tablesBySourceKey.TryGetValue(key, out var table))
                    {
                        logger.LogError("Skipping {Table}: not present in metadata.json.", key);
                        lock (resultsLock) { results.Add((key, false, 0, "not present in metadata.json")); }
                        return;
                    }

                    await throttle.WaitAsync(cancellationToken);
                    try
                    {
                        var targetFileName = PostgresIdentifiers.Normalize(entry.TargetTable, idCase) + ".csv";
                        var targetPath = Path.Combine(outputRoot, targetFileName);
                        var orderedColumns = table.Columns.OrderBy(c => c.OrdinalPosition).ToList();
                        var headerNames = orderedColumns.Select(c => PostgresIdentifiers.Normalize(c.Name, idCase)).ToList();

                        Console.WriteLine($"Exporting {key} -> {targetPath}");
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        var convertedRows = ConvertRows(dataReader.StreamRowsAsync(table.Schema, table.Name, orderedColumns, cancellationToken), orderedColumns, converter);
                        var rowCount = await csvWriter.WriteAsync(targetPath, headerNames, convertedRows, cancellationToken);

                        sw.Stop();
                        var rate = sw.Elapsed.TotalSeconds > 0 ? rowCount / sw.Elapsed.TotalSeconds : rowCount;
                        Console.WriteLine($"Done {key}: {rowCount:N0} rows in {sw.Elapsed:hh\\:mm\\:ss} ({rate:N0} rows/sec)");

                        var loadScript = copyScriptGenerator.GenerateForTable(entry.TargetSchema, entry.TargetTable, $"../data/{targetFileName}");
                        await AtomicFile.WriteTextAsync(Path.Combine(loadDirectory, $"{PostgresIdentifiers.Normalize(entry.TargetTable, idCase)}.sql"), loadScript, cancellationToken);

                        lock (stateLock)
                        {
                            state.Tables[key] = new TableExportState(TableExportStatus.Complete, rowCount, DateTimeOffset.UtcNow);
                        }
                        lock (resultsLock) { results.Add((key, true, rowCount, null)); }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Export failed for {Table}", key);
                        lock (stateLock)
                        {
                            state.Tables[key] = new TableExportState(TableExportStatus.Failed, null, DateTimeOffset.UtcNow);
                        }
                        lock (resultsLock) { results.Add((key, false, 0, ex.Message)); }
                    }
                    finally
                    {
                        throttle.Release();
                    }
                });

                await Task.WhenAll(tasks);
                await MigrationStateIo.WriteAsync(statePath, state, cancellationToken);

                var succeeded = results.Where(r => r.Success).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (succeeded.Count > 0)
                {
                    var order = TableDependencyGraph.Order(schema.Tables).OrderedTables
                        .Where(succeeded.Contains)
                        .Select(key => entries.First(e => string.Equals($"{e.SourceSchema}.{e.SourceTable}", key, StringComparison.OrdinalIgnoreCase)))
                        .Select(e => $"{PostgresIdentifiers.Normalize(e.TargetTable, idCase)}.sql")
                        .ToList();

                    var loadAll = copyScriptGenerator.GenerateLoadAll(order);
                    await AtomicFile.WriteTextAsync(Path.Combine(loadDirectory, "load-all.sql"), loadAll, cancellationToken);
                }

                var failures = results.Where(r => !r.Success).ToList();
                foreach (var failure in failures)
                {
                    Console.Error.WriteLine($"FAILED: {failure.Key}: {failure.Error}");
                }

                return failures.Count > 0 ? ExitCodes.GeneralFailure : ExitCodes.Success;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.InvalidArguments;
            }
            catch (ManifestValidationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.InvalidArguments;
            }
            catch (OutputDirectoryException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.OutputStorageFailure;
            }
        });

        return command;
    }

    private static async IAsyncEnumerable<object?[]> ConvertRows(
        IAsyncEnumerable<object?[]> rawRows,
        IReadOnlyList<DatabaseColumn> columns,
        ISourceValueConverter converter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in rawRows.WithCancellation(cancellationToken))
        {
            var converted = new object?[row.Length];
            for (var i = 0; i < row.Length; i++)
            {
                converted[i] = converter.Convert(columns[i].SourceType, row[i]);
            }
            yield return converted;
        }
    }
}

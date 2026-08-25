using System.CommandLine;
using Microsoft.Extensions.Logging;
using OraPgMigrator.Cli.Options;
using OraPgMigrator.Core.Mapping;
using OraPgMigrator.Core.Migration;
using OraPgMigrator.Core.Validation;
using OraPgMigrator.Infrastructure.Csv;
using OraPgMigrator.Infrastructure.FileSystem;
using OraPgMigrator.Infrastructure.Json;
using OraPgMigrator.Postgres.Ddl;
using OraPgMigrator.Postgres.Loader;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Cli.Commands;

/// <summary>
/// `orapg ddl`: reads manifest.csv + metadata.json and generates per-table
/// PostgreSQL DDL (spec §17-§21). Never reconnects to Oracle.
/// </summary>
public static class DdlCommand
{
    public static Command Build()
    {
        var manifest = CommonOptions.Manifest();
        var metadata = CommonOptions.Metadata();
        var output = CommonOptions.Output();
        var typeMap = CommonOptions.TypeMap();
        var identifierCase = CommonOptions.IdentifierCase();
        var logLevel = CommonOptions.LogLevel();
        var logFile = CommonOptions.LogFile();

        var command = new Command("ddl", "Generate PostgreSQL DDL from manifest.csv and metadata.json.");
        command.Options.Add(manifest);
        command.Options.Add(metadata);
        command.Options.Add(output);
        command.Options.Add(typeMap);
        command.Options.Add(identifierCase);
        command.Options.Add(logLevel);
        command.Options.Add(logFile);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var logger = CliHost.CreateLogger(parseResult, logLevel, logFile);

            try
            {
                var idCase = parseResult.GetValue(identifierCase);
                var outputRoot = OutputPathNormalizer.Normalize(parseResult.GetRequiredValue(output));
                OutputDirectoryValidator.EnsureWritable(outputRoot);

                var manifestDoc = ManifestCsvIo.Read(parseResult.GetRequiredValue(manifest));
                var schema = await MetadataJsonSerializer.ReadAsync(parseResult.GetRequiredValue(metadata), cancellationToken);

                var typeMapPath = parseResult.GetValue(typeMap);
                var overrides = typeMapPath is not null
                    ? await Infrastructure.Json.TypeMapJsonReader.ReadAsync(typeMapPath, cancellationToken)
                    : TypeMappingOverrides.Empty;

                var typeMapper = new OracleToPostgresTypeMapper(overrides);
                var generator = new PostgresDdlGenerator(typeMapper, idCase);
                var schemaGenerator = new SchemaDdlGenerator(idCase);
                var sequenceGenerator = new SequenceDdlGenerator(idCase);

                var tablesBySourceKey = schema.Tables.ToDictionary(t => Key(t.Schema, t.Name), StringComparer.OrdinalIgnoreCase);
                var targetBySourceKey = manifestDoc.Entries.ToDictionary(
                    e => Key(e.SourceSchema, e.SourceTable),
                    e => (e.TargetSchema, e.TargetTable),
                    StringComparer.OrdinalIgnoreCase);

                var entries = manifestDoc.ForDdl().ToList();
                var allIssues = new List<ValidationIssue>();
                var writtenSchemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int tablesGenerated = 0, tablesFailed = 0;

                Directory.CreateDirectory(Path.Combine(outputRoot, "schemas"));
                Directory.CreateDirectory(Path.Combine(outputRoot, "tables"));
                Directory.CreateDirectory(Path.Combine(outputRoot, "primary-keys"));
                Directory.CreateDirectory(Path.Combine(outputRoot, "foreign-keys"));
                Directory.CreateDirectory(Path.Combine(outputRoot, "indexes"));
                Directory.CreateDirectory(Path.Combine(outputRoot, "sequences"));

                foreach (var entry in entries)
                {
                    if (!tablesBySourceKey.TryGetValue(Key(entry.SourceSchema, entry.SourceTable), out var table))
                    {
                        allIssues.Add(new ValidationIssue(ValidationSeverity.Error, "MissingMetadata", $"{entry.SourceSchema}.{entry.SourceTable} is in manifest.csv but not in metadata.json."));
                        tablesFailed++;
                        continue;
                    }

                    if (writtenSchemas.Add(entry.TargetSchema))
                    {
                        var schemaDdl = schemaGenerator.Generate(entry.TargetSchema);
                        await WriteSql(Path.Combine(outputRoot, "schemas", schemaDdl.FileBaseName + ".sql"), schemaDdl.Sql!, cancellationToken);
                    }

                    var tableResult = generator.GenerateTable(table, entry.TargetSchema, entry.TargetTable);
                    allIssues.AddRange(tableResult.Issues);

                    if (!tableResult.Success)
                    {
                        logger.LogError("Skipped DDL for {Schema}.{Table}: unsupported column type(s).", entry.SourceSchema, entry.SourceTable);
                        tablesFailed++;
                        continue;
                    }

                    await WriteSql(Path.Combine(outputRoot, "tables", tableResult.FileBaseName + ".sql"), tableResult.Sql!, cancellationToken);
                    tablesGenerated++;

                    var pk = generator.GeneratePrimaryKey(table, entry.TargetSchema, entry.TargetTable);
                    allIssues.AddRange(pk.Issues);
                    if (pk.Success)
                    {
                        await WriteSql(Path.Combine(outputRoot, "primary-keys", pk.FileBaseName + ".sql"), pk.Sql!, cancellationToken);
                    }

                    var fk = generator.GenerateForeignKeys(table, entry.TargetSchema, entry.TargetTable, (refSchema, refTable) =>
                    {
                        if (targetBySourceKey.TryGetValue(Key(refSchema, refTable), out var target))
                        {
                            return (target.TargetSchema, target.TargetTable);
                        }
                        allIssues.Add(new ValidationIssue(ValidationSeverity.Warning, "ForeignKeyTarget", $"{entry.SourceTable}: referenced table {refSchema}.{refTable} is not in the manifest; using its normalized source name."));
                        return (entry.TargetSchema, PostgresIdentifiers.Normalize(refTable, idCase));
                    });
                    allIssues.AddRange(fk.Issues);
                    if (fk.Success)
                    {
                        await WriteSql(Path.Combine(outputRoot, "foreign-keys", fk.FileBaseName + ".sql"), fk.Sql!, cancellationToken);
                    }

                    var indexes = generator.GenerateIndexes(table, entry.TargetSchema, entry.TargetTable);
                    allIssues.AddRange(indexes.Issues);
                    if (indexes.Success)
                    {
                        await WriteSql(Path.Combine(outputRoot, "indexes", indexes.FileBaseName + ".sql"), indexes.Sql!, cancellationToken);
                    }
                }

                var defaultTargetSchema = entries.Select(e => e.TargetSchema).FirstOrDefault() ?? "public";
                foreach (var sequence in schema.Sequences)
                {
                    var seqDdl = sequenceGenerator.Generate(sequence, defaultTargetSchema);
                    await WriteSql(Path.Combine(outputRoot, "sequences", seqDdl.FileBaseName + ".sql"), seqDdl.Sql!, cancellationToken);
                }

                foreach (var issue in allIssues)
                {
                    var line = $"[{issue.Severity}] [{issue.Category}] {issue.Message}";
                    if (issue.Severity == ValidationSeverity.Error)
                    {
                        Console.Error.WriteLine(line);
                    }
                    else
                    {
                        Console.WriteLine(line);
                    }
                }

                Console.WriteLine($"Generated DDL for {tablesGenerated} table(s); {tablesFailed} table(s) skipped due to unsupported types or missing metadata.");

                return tablesFailed > 0 ? ExitCodes.UnsupportedSchemaOrData : ExitCodes.Success;
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

    private static async Task WriteSql(string path, string content, CancellationToken cancellationToken) =>
        await AtomicFile.WriteTextAsync(path, content, cancellationToken);

    private static string Key(string schema, string table) => $"{schema}.{table}";
}

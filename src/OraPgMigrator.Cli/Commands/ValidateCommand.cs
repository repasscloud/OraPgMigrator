using System.CommandLine;
using OraPgMigrator.Cli.Options;
using OraPgMigrator.Core.Abstractions;
using OraPgMigrator.Infrastructure;
using OraPgMigrator.Infrastructure.Csv;
using OraPgMigrator.Oracle.SchemaReader;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Cli.Commands;

/// <summary>
/// `orapg validate`: row-count validation between Oracle and PostgreSQL for every
/// exported table (spec §36). This is intentionally row-count-only for the MVP —
/// stronger chunk-hash validation (spec §37) is not implemented yet and must not
/// be implied by this command's output.
/// </summary>
public static class ValidateCommand
{
    public static Command Build()
    {
        var oracleOptions = new OracleConnectionOptionSet();
        var manifest = CommonOptions.Manifest();
        var postgresConnection = new Option<string?>("--postgres-connection") { Description = "PostgreSQL connection string." };
        var postgresConnectionEnv = new Option<string?>("--postgres-connection-env") { Description = "Environment variable containing the PostgreSQL connection string." };
        var logLevel = CommonOptions.LogLevel();
        var logFile = CommonOptions.LogFile();

        var command = new Command("validate", "Compare Oracle and PostgreSQL row counts for every table selected for export.");
        oracleOptions.AddTo(command);
        command.Options.Add(manifest);
        command.Options.Add(postgresConnection);
        command.Options.Add(postgresConnectionEnv);
        command.Options.Add(logLevel);
        command.Options.Add(logFile);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            CliHost.CreateLogger(parseResult, logLevel, logFile);
            var envResolver = new EnvironmentVariableResolver();

            try
            {
                var oracleSettings = oracleOptions.Resolve(parseResult, envResolver);
                var pgConnectionString = ResolvePostgresConnectionString(parseResult, postgresConnection, postgresConnectionEnv, envResolver);

                var manifestDoc = ManifestCsvIo.Read(parseResult.GetRequiredValue(manifest));
                var entries = manifestDoc.ForExport().ToList();

                var oracleCounter = new OracleRowCounter(oracleSettings);
                var pgCounter = new PostgresRowCounter(pgConnectionString);

                Console.WriteLine($"{"Table",-30} {"Oracle",15} {"PostgreSQL",15}   Result");
                Console.WriteLine(new string('-', 75));

                var anyFailure = false;
                foreach (var entry in entries)
                {
                    var oracleCount = await oracleCounter.CountAsync(entry.SourceSchema, entry.SourceTable, cancellationToken);
                    var pgCount = await pgCounter.CountAsync(entry.TargetSchema, entry.TargetTable, cancellationToken);
                    var ok = oracleCount == pgCount;
                    anyFailure |= !ok;

                    Console.WriteLine($"{entry.SourceTable,-30} {oracleCount,15:N0} {pgCount,15:N0}   {(ok ? "OK" : "FAIL")}");
                }

                return anyFailure ? ExitCodes.ValidationFailure : ExitCodes.Success;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.InvalidArguments;
            }
            catch (Core.Migration.ManifestValidationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.InvalidArguments;
            }
        });

        return command;
    }

    private static string ResolvePostgresConnectionString(
        ParseResult parseResult, Option<string?> direct, Option<string?> directEnv, IEnvironmentVariableResolver envResolver)
    {
        var value = parseResult.GetValue(direct);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        var envName = parseResult.GetValue(directEnv);
        if (!string.IsNullOrEmpty(envName))
        {
            return envResolver.GetRequired(envName);
        }

        throw new InvalidOperationException("PostgreSQL connection was not provided. Supply --postgres-connection or --postgres-connection-env.");
    }
}

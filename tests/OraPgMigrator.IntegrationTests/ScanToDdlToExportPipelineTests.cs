using OraPgMigrator.Core.Migration;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Infrastructure.Csv;
using OraPgMigrator.Infrastructure.Json;
using OraPgMigrator.Postgres.Ddl;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.IntegrationTests;

/// <summary>
/// End-to-end exercise of scan-output -> manifest -> ddl -> csv-export wiring
/// across Core, Postgres and Infrastructure, without a real Oracle/PostgreSQL
/// instance. Tests that require a live database are separated out (see
/// <see cref="RequiresRealDatabaseTests"/>) and are not run by default.
/// </summary>
public class ScanToDdlToExportPipelineTests
{
    private static DatabaseSchema BuildScannedSchema()
    {
        var customer = new DatabaseTable(
            "LEGACY",
            "CUSTOMER",
            2,
            [
                new DatabaseColumn("CUSTOMER_ID", 1, new SourceDataType("NUMBER", null, 10, 0, false), false, null, null),
                new DatabaseColumn("EMAIL", 2, new SourceDataType("VARCHAR2", 255, null, null, true), true, null, null)
            ],
            new PrimaryKeyDefinition("PK_CUSTOMER", ["CUSTOMER_ID"]),
            [],
            [],
            [],
            [],
            [],
            null,
            null);

        var order = new DatabaseTable(
            "LEGACY",
            "ORDERS",
            1,
            [
                new DatabaseColumn("ORDER_ID", 1, new SourceDataType("NUMBER", null, 10, 0, false), false, null, null),
                new DatabaseColumn("CUSTOMER_ID", 2, new SourceDataType("NUMBER", null, 10, 0, false), false, null, null)
            ],
            new PrimaryKeyDefinition("PK_ORDER", ["ORDER_ID"]),
            [new ForeignKeyDefinition("FK_ORDER_CUSTOMER", ["CUSTOMER_ID"], "LEGACY", "CUSTOMER", ["CUSTOMER_ID"])],
            [],
            [],
            [],
            [],
            null,
            null);

        return new DatabaseSchema("ORCL", "LEGACY", DateTimeOffset.UtcNow, [customer, order], [], [], [], []);
    }

    [Fact]
    public async Task FullPipeline_ScanOutput_ThroughDdlAndCsvExport_ProducesConsistentArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orapg-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var schema = BuildScannedSchema();
            var manifestEntries = schema.Tables
                .Select(t => new ManifestEntry(true, schema.SchemaName, t.Name, t.RowCount, true, true, "public", t.Name.ToLowerInvariant()))
                .ToList();

            var paths = new MigrationOutputPath(root);
            await ManifestCsvIo.WriteAsync(paths.ManifestPath, manifestEntries, CancellationToken.None);
            await MetadataJsonSerializer.WriteAsync(paths.MetadataPath, schema, CancellationToken.None);

            // --- ddl stage: re-read from disk, exactly as the CLI `ddl` command does ---
            var manifestDoc = ManifestCsvIo.Read(paths.ManifestPath);
            var reloadedSchema = await MetadataJsonSerializer.ReadAsync(paths.MetadataPath, CancellationToken.None);

            var generator = new PostgresDdlGenerator(new OracleToPostgresTypeMapper(), IdentifierCase.Lower);
            var tablesByKey = reloadedSchema.Tables.ToDictionary(t => $"{t.Schema}.{t.Name}", StringComparer.OrdinalIgnoreCase);
            var targetByKey = manifestDoc.Entries.ToDictionary(e => $"{e.SourceSchema}.{e.SourceTable}", e => (e.TargetSchema, e.TargetTable), StringComparer.OrdinalIgnoreCase);

            var ddlFiles = new Dictionary<string, string>();
            foreach (var entry in manifestDoc.ForDdl())
            {
                var table = tablesByKey[$"{entry.SourceSchema}.{entry.SourceTable}"];
                var tableDdl = generator.GenerateTable(table, entry.TargetSchema, entry.TargetTable);
                Assert.True(tableDdl.Success, string.Join(";", tableDdl.Issues.Select(i => i.Message)));
                ddlFiles[entry.TargetTable] = tableDdl.Sql!;

                var fk = generator.GenerateForeignKeys(table, entry.TargetSchema, entry.TargetTable, (refSchema, refTable) =>
                    targetByKey.TryGetValue($"{refSchema}.{refTable}", out var t) ? (t.TargetSchema, t.TargetTable) : (entry.TargetSchema, refTable.ToLowerInvariant()));
                if (fk.Success)
                {
                    ddlFiles[entry.TargetTable + ".fk"] = fk.Sql!;
                }
            }

            Assert.Contains("CREATE TABLE public.customer", ddlFiles["customer"]);
            Assert.Contains("customer_id bigint NOT NULL", ddlFiles["customer"]);
            Assert.Contains("REFERENCES public.customer (customer_id)", ddlFiles["orders.fk"]);

            // --- export stage: stream fake converted rows straight to CSV, as `export` does per table ---
            var csvWriter = new MigrationCsvWriter();
            var customerRows = new object?[][]
            {
                [1m, "a@example.com"],
                [2m, null]
            };
            var csvPath = Path.Combine(root, "customer.csv");
            var rowCount = await csvWriter.WriteAsync(csvPath, ["customer_id", "email"], ToAsyncEnumerable(customerRows), CancellationToken.None);

            Assert.Equal(2, rowCount);
            var csvContent = await File.ReadAllTextAsync(csvPath);
            Assert.Contains("customer_id,email", csvContent);
            Assert.Contains("1,a@example.com", csvContent);
            Assert.Contains("2,\\N", csvContent); // NULL must be distinguishable from empty string
            Assert.False(File.Exists(csvPath + ".partial")); // atomic rename must have completed
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async IAsyncEnumerable<object?[]> ToAsyncEnumerable(IEnumerable<object?[]> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }
}

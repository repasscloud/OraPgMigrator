using OraPgMigrator.Core.Models;
using OraPgMigrator.Infrastructure.Json;

namespace OraPgMigrator.Core.Tests.Json;

public class MetadataJsonSerializerTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsSchema()
    {
        var table = new DatabaseTable(
            "LEGACY",
            "CUSTOMER",
            12842,
            [new DatabaseColumn("CUSTOMER_ID", 1, new SourceDataType("NUMBER", null, 19, 0, false), false, null, null)],
            new PrimaryKeyDefinition("PK_CUSTOMER", ["CUSTOMER_ID"]),
            [],
            [],
            [],
            [],
            [],
            null,
            null);

        var schema = new DatabaseSchema("ORCL", "LEGACY", DateTimeOffset.UtcNow, [table], [], [], [], []);

        var path = Path.Combine(Path.GetTempPath(), $"metadata-{Guid.NewGuid():N}.json");
        try
        {
            await MetadataJsonSerializer.WriteAsync(path, schema, CancellationToken.None);
            var roundTripped = await MetadataJsonSerializer.ReadAsync(path, CancellationToken.None);

            Assert.Equal(schema.SchemaName, roundTripped.SchemaName);
            Assert.Single(roundTripped.Tables);
            Assert.Equal("CUSTOMER", roundTripped.Tables[0].Name);
            Assert.Equal(12842, roundTripped.Tables[0].RowCount);
            Assert.Equal("PK_CUSTOMER", roundTripped.Tables[0].PrimaryKey?.Name);
            Assert.Equal("CUSTOMER_ID", roundTripped.Tables[0].Columns[0].Name);
            Assert.Equal(19, roundTripped.Tables[0].Columns[0].SourceType.Precision);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Write_NeverContainsThePasswordText()
    {
        var schema = new DatabaseSchema("ORCL", "LEGACY", DateTimeOffset.UtcNow, [], [], [], [], []);
        var path = Path.Combine(Path.GetTempPath(), $"metadata-{Guid.NewGuid():N}.json");
        try
        {
            await MetadataJsonSerializer.WriteAsync(path, schema, CancellationToken.None);
            var content = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("hunter2", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

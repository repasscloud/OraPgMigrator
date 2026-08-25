using OraPgMigrator.Core.Migration;
using OraPgMigrator.Infrastructure.Csv;

namespace OraPgMigrator.Core.Tests.Csv;

public class ManifestCsvIoTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsAllFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.csv");
        var entries = new List<ManifestEntry>
        {
            new(true, "LEGACY", "CUSTOMER", 12342, true, true, "public", "customer"),
            new(false, "LEGACY", "AUDIT_ARCHIVE", 82444512, false, false, "public", "audit_archive")
        };

        try
        {
            await ManifestCsvIo.WriteAsync(path, entries, CancellationToken.None);
            var doc = ManifestCsvIo.Read(path);

            Assert.Equal(2, doc.Entries.Count);
            Assert.Equal(entries[0], doc.Entries[0]);
            Assert.Equal(entries[1], doc.Entries[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ToleratesReorderedColumns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path,
            "SourceTable,Include,TargetTable,SourceSchema,RowCount,GenerateDDL,ExportData,TargetSchema\r\n" +
            "CUSTOMER,true,customer,LEGACY,100,true,true,public\r\n");

        try
        {
            var doc = ManifestCsvIo.Read(path);
            var entry = Assert.Single(doc.Entries);
            Assert.Equal("CUSTOMER", entry.SourceTable);
            Assert.Equal("LEGACY", entry.SourceSchema);
            Assert.Equal(100, entry.RowCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingRequiredColumn_ThrowsManifestValidationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Include,SourceSchema,SourceTable\r\ntrue,LEGACY,CUSTOMER\r\n");

        try
        {
            Assert.Throws<ManifestValidationException>(() => ManifestCsvIo.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MissingFile_ThrowsManifestValidationException()
    {
        Assert.Throws<ManifestValidationException>(() => ManifestCsvIo.Read(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv")));
    }
}

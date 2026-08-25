using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OraPgMigrator.Core.Migration;
using OraPgMigrator.Infrastructure.FileSystem;

namespace OraPgMigrator.Infrastructure.Csv;

/// <summary>
/// Reads and writes manifest.csv. Reading is column-name based (not positional) so
/// commands tolerate a user reordering columns, and validates required columns are
/// present up front with a clear error (spec §8).
/// </summary>
public static class ManifestCsvIo
{
    public static ManifestDocument Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new ManifestValidationException($"Manifest file not found: {path}");
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { TrimOptions = TrimOptions.Trim });

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new ManifestValidationException($"Manifest file is empty: {path}");
        }

        ManifestDocument.ValidateHeader(csv.HeaderRecord ?? []);

        var entries = new List<ManifestEntry>();
        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            try
            {
                entries.Add(new ManifestEntry(
                    csv.GetField<bool>(ManifestColumns.Include),
                    csv.GetField(ManifestColumns.SourceSchema) ?? string.Empty,
                    csv.GetField(ManifestColumns.SourceTable) ?? string.Empty,
                    csv.GetField<long>(ManifestColumns.RowCount),
                    csv.GetField<bool>(ManifestColumns.GenerateDDL),
                    csv.GetField<bool>(ManifestColumns.ExportData),
                    csv.GetField(ManifestColumns.TargetSchema) ?? string.Empty,
                    csv.GetField(ManifestColumns.TargetTable) ?? string.Empty));
            }
            catch (Exception ex) when (ex is CsvHelperException or FormatException)
            {
                throw new ManifestValidationException($"Manifest row {rowNumber} is malformed: {ex.Message}");
            }
        }

        return new ManifestDocument(entries);
    }

    public static async Task WriteAsync(string path, IReadOnlyList<ManifestEntry> entries, CancellationToken cancellationToken)
    {
        await AtomicFile.WriteAsync(path, async (stream, ct) =>
        {
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
            await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

            foreach (var column in ManifestColumns.Required)
            {
                csv.WriteField(column);
            }
            await csv.NextRecordAsync();

            foreach (var entry in entries)
            {
                csv.WriteField(entry.Include);
                csv.WriteField(entry.SourceSchema);
                csv.WriteField(entry.SourceTable);
                csv.WriteField(entry.RowCount);
                csv.WriteField(entry.GenerateDdl);
                csv.WriteField(entry.ExportData);
                csv.WriteField(entry.TargetSchema);
                csv.WriteField(entry.TargetTable);
                await csv.NextRecordAsync();
            }

            await writer.FlushAsync(ct);
        }, cancellationToken);
    }
}

using OraPgMigrator.Infrastructure.FileSystem;

namespace OraPgMigrator.Infrastructure.Csv;

/// <summary>
/// Streams table export rows to a CSV file. Never buffers the full result set:
/// each row is written and the underlying stream is periodically flushed. Writes
/// to a <c>.partial</c> file first (see <see cref="AtomicFile"/>) so a crash or
/// cancellation never leaves a file that looks like a completed export.
/// </summary>
public sealed class MigrationCsvWriter
{
    private const int FlushEveryRows = 5000;

    public async Task<long> WriteAsync(
        string finalPath,
        IReadOnlyList<string> headerColumnNames,
        IAsyncEnumerable<object?[]> rows,
        CancellationToken cancellationToken)
    {
        long rowCount = 0;

        await AtomicFile.WriteAsync(finalPath, async (stream, ct) =>
        {
            var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
            await using (writer)
            {
                WriteHeader(writer, headerColumnNames);

                var sinceFlush = 0;
                await foreach (var row in rows.WithCancellation(ct))
                {
                    WriteRow(writer, row);
                    rowCount++;
                    sinceFlush++;
                    if (sinceFlush >= FlushEveryRows)
                    {
                        await writer.FlushAsync(ct);
                        sinceFlush = 0;
                    }
                }

                await writer.FlushAsync(ct);
            }
        }, cancellationToken);

        return rowCount;
    }

    private static void WriteHeader(TextWriter writer, IReadOnlyList<string> columnNames)
    {
        for (var i = 0; i < columnNames.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }
            CsvFieldFormatter.WriteField(writer, columnNames[i]);
        }
        writer.Write('\n');
    }

    private static void WriteRow(TextWriter writer, object?[] row)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }
            CsvFieldFormatter.WriteField(writer, row[i]);
        }
        writer.Write('\n');
    }
}

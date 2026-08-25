using System.Globalization;
using System.Text;

namespace OraPgMigrator.Infrastructure.Csv;

/// <summary>
/// Formats a single converted CLR value (see <c>ISourceValueConverter</c>) as a
/// CSV field ready for PostgreSQL <c>COPY ... WITH (FORMAT csv, NULL '\N')</c>.
/// Always uses invariant culture — the host machine's regional settings must
/// never influence exported values (spec §25-§27).
/// </summary>
public static class CsvFieldFormatter
{
    public const string NullToken = "\\N";

    /// <summary>Writes one formatted, already-quoted-if-necessary field to <paramref name="writer"/>.</summary>
    public static void WriteField(TextWriter writer, object? value)
    {
        if (value is null)
        {
            writer.Write(NullToken);
            return;
        }

        var text = FormatValue(value);
        WriteQuotedIfNeeded(writer, text);
    }

    private static string FormatValue(object value) => value switch
    {
        string s => s,
        bool b => b ? "t" : "f",
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        int or long or short => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        DateTimeOffset dto => FormatDateTimeOffset(dto),
        DateTime dt => FormatDateTime(dt),
        TimeSpan ts => FormatInterval(ts),
        byte[] bytes => "\\x" + System.Convert.ToHexString(bytes).ToLowerInvariant(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatDateTime(DateTime dt)
    {
        var basePart = dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var fraction = dt.Ticks % TimeSpan.TicksPerSecond;
        if (fraction == 0)
        {
            return basePart;
        }

        var microseconds = fraction / 10; // 100ns ticks -> microseconds
        return $"{basePart}.{microseconds.ToString("D6", CultureInfo.InvariantCulture).TrimEnd('0')}";
    }

    private static string FormatDateTimeOffset(DateTimeOffset dto)
    {
        var basePart = FormatDateTime(dto.DateTime);
        var offset = dto.Offset;
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"{basePart}{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
    }

    private static string FormatInterval(TimeSpan ts)
    {
        var sign = ts < TimeSpan.Zero ? "-" : string.Empty;
        var abs = ts.Duration();
        return $"{sign}{abs.Days} days {abs.Hours:D2}:{abs.Minutes:D2}:{abs.Seconds:D2}";
    }

    private static void WriteQuotedIfNeeded(TextWriter writer, string text)
    {
        var needsQuoting = text.Length == 0
            ? false
            : text.AsSpan().IndexOfAny(',', '"') >= 0
              || text.Contains('\n')
              || text.Contains('\r')
              || string.Equals(text, NullToken, StringComparison.Ordinal);

        if (!needsQuoting)
        {
            writer.Write(text);
            return;
        }

        writer.Write('"');
        foreach (var ch in text)
        {
            if (ch == '"')
            {
                writer.Write('"');
            }
            writer.Write(ch);
        }
        writer.Write('"');
    }
}

using System.Globalization;
using OraPgMigrator.Infrastructure.Csv;

namespace OraPgMigrator.Core.Tests.Csv;

public class CsvFieldFormatterTests
{
    private static string Format(object? value)
    {
        using var writer = new StringWriter();
        CsvFieldFormatter.WriteField(writer, value);
        return writer.ToString();
    }

    [Fact]
    public void Null_WritesNullToken() => Assert.Equal("\\N", Format(null));

    [Fact]
    public void EmptyString_IsDistinguishableFromNull() => Assert.Equal(string.Empty, Format(string.Empty));

    [Fact]
    public void LiteralNullTokenValue_IsQuotedToDisambiguateFromNull() => Assert.Equal("\"\\N\"", Format("\\N"));

    [Fact]
    public void StringWithComma_IsQuoted() => Assert.Equal("\"a,b\"", Format("a,b"));

    [Fact]
    public void StringWithQuote_IsQuotedAndEscaped() => Assert.Equal("\"a\"\"b\"", Format("a\"b"));

    [Fact]
    public void StringWithNewline_IsQuoted() => Assert.Equal("\"a\nb\"", Format("a\nb"));

    [Fact]
    public void PlainString_IsNotQuoted() => Assert.Equal("hello", Format("hello"));

    [Fact]
    public void Decimal_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); // uses comma decimal separator
            Assert.Equal("1234.5", Format(1234.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void DateTime_WithoutFraction_FormatsAsSeconds() =>
        Assert.Equal("2026-08-25 13:45:01", Format(new DateTime(2026, 8, 25, 13, 45, 1)));

    [Fact]
    public void DateTime_WithFraction_IncludesMicroseconds() =>
        Assert.Equal("2026-08-25 13:45:01.123", Format(new DateTime(2026, 8, 25, 13, 45, 1).AddMilliseconds(123)));

    [Fact]
    public void DateTimeOffset_IncludesOffset() =>
        Assert.Equal("2026-08-25 13:45:01+09:30", Format(new DateTimeOffset(2026, 8, 25, 13, 45, 1, TimeSpan.FromMinutes(9 * 60 + 30))));

    [Fact]
    public void ByteArray_FormatsAsPostgresHexBytea() =>
        Assert.Equal("\\x0a1b", Format(new byte[] { 0x0A, 0x1B }));

    [Fact]
    public void Bool_FormatsAsSingleCharacter()
    {
        Assert.Equal("t", Format(true));
        Assert.Equal("f", Format(false));
    }
}

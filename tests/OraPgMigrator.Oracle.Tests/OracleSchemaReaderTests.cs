using OraPgMigrator.Oracle.SchemaReader;

namespace OraPgMigrator.Oracle.Tests;

public class OracleSchemaReaderTests
{
    [Fact]
    public void ToClampedInt64_ValueWithinRange_ConvertsExactly() =>
        Assert.Equal(42L, OracleSchemaReader.ToClampedInt64(42m));

    [Fact]
    public void ToClampedInt64_OracleDefaultMaxValue_ClampsToInt64Max() =>
        Assert.Equal(long.MaxValue, OracleSchemaReader.ToClampedInt64(9999999999999999999999999999m));

    [Fact]
    public void ToClampedInt64_OracleDefaultDescendingMinValue_ClampsToInt64Min() =>
        Assert.Equal(long.MinValue, OracleSchemaReader.ToClampedInt64(-999999999999999999999999999m));

    [Fact]
    public void ToExactInt64_ValueWithinRange_ConvertsExactly() =>
        Assert.Equal(42L, OracleSchemaReader.ToExactInt64(42m, "SEQ", "LAST_NUMBER"));

    [Fact]
    public void ToExactInt64_OutOfRangeOperationalValue_ThrowsInsteadOfClamping()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => OracleSchemaReader.ToExactInt64(10000000000000000000m, "SEQ", "INCREMENT_BY"));

        Assert.Contains("SEQ", ex.Message);
        Assert.Contains("INCREMENT_BY", ex.Message);
    }
}

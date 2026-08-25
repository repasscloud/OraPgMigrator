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
}

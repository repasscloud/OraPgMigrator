using OraPgMigrator.Core.Mapping;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Postgres.Ddl;

namespace OraPgMigrator.Postgres.Tests.Ddl;

public class OracleToPostgresTypeMapperTests
{
    private readonly OracleToPostgresTypeMapper _mapper = new();

    private static SourceDataType Number(int? precision, int? scale) => new("NUMBER", null, precision, scale, false);

    [Theory]
    [InlineData(1, "smallint")]
    [InlineData(4, "smallint")]
    [InlineData(5, "integer")]
    [InlineData(9, "integer")]
    [InlineData(10, "bigint")]
    [InlineData(18, "bigint")]
    public void Number_WithoutScale_MapsToIntegerFamily(int precision, string expected)
    {
        var result = _mapper.Map(Number(precision, 0));
        Assert.Equal(expected, result.SqlType);
    }

    [Fact]
    public void Number_WithLargePrecisionNoScale_MapsToNumericWithPrecision()
    {
        var result = _mapper.Map(Number(38, 0));
        Assert.Equal("numeric(38)", result.SqlType);
    }

    [Fact]
    public void Number_WithScale_MapsToNumericPrecisionScale()
    {
        var result = _mapper.Map(Number(10, 2));
        Assert.Equal("numeric(10,2)", result.SqlType);
    }

    [Fact]
    public void Number_WithNoPrecision_MapsToPlainNumeric()
    {
        var result = _mapper.Map(Number(null, null));
        Assert.Equal("numeric", result.SqlType);
    }

    [Fact]
    public void Varchar2_WithLength_IncludesLength()
    {
        var result = _mapper.Map(new SourceDataType("VARCHAR2", 100, null, null, true));
        Assert.Equal("varchar(100)", result.SqlType);
    }

    [Fact]
    public void Varchar2_WithoutLength_MapsToText()
    {
        var result = _mapper.Map(new SourceDataType("VARCHAR2", null, null, null, true));
        Assert.Equal("text", result.SqlType);
    }

    [Theory]
    [InlineData("CLOB", "text")]
    [InlineData("NCLOB", "text")]
    [InlineData("LONG", "text")]
    [InlineData("BLOB", "bytea")]
    [InlineData("RAW", "bytea")]
    [InlineData("LONG RAW", "bytea")]
    [InlineData("DATE", "timestamp without time zone")]
    [InlineData("TIMESTAMP", "timestamp without time zone")]
    [InlineData("TIMESTAMP WITH TIME ZONE", "timestamp with time zone")]
    [InlineData("BINARY_FLOAT", "real")]
    [InlineData("BINARY_DOUBLE", "double precision")]
    [InlineData("FLOAT", "double precision")]
    [InlineData("INTERVAL YEAR TO MONTH", "interval")]
    [InlineData("INTERVAL DAY TO SECOND", "interval")]
    public void KnownTypes_MapAsExpected(string oracleType, string expected)
    {
        var result = _mapper.Map(new SourceDataType(oracleType, null, null, null, false));
        Assert.Equal(expected, result.SqlType);
    }

    [Fact]
    public void UnsupportedType_ThrowsUnsupportedSourceTypeException()
    {
        Assert.Throws<UnsupportedSourceTypeException>(() =>
            _mapper.Map(new SourceDataType("SDO_GEOMETRY", null, null, null, false)));
    }

    [Fact]
    public void XmlType_ThrowsUnsupportedSourceTypeException()
    {
        Assert.Throws<UnsupportedSourceTypeException>(() =>
            _mapper.Map(new SourceDataType("XMLTYPE", null, null, null, false)));
    }

    [Fact]
    public void Override_TakesPrecedenceOverBuiltInRule()
    {
        var overrides = new TypeMappingOverrides(new Dictionary<string, string> { ["XMLTYPE"] = "xml" });
        var mapper = new OracleToPostgresTypeMapper(overrides);
        var result = mapper.Map(new SourceDataType("XMLTYPE", null, null, null, false));
        Assert.Equal("xml", result.SqlType);
    }

    [Fact]
    public void Override_IsCaseInsensitiveOnNativeTypeName()
    {
        var overrides = new TypeMappingOverrides(new Dictionary<string, string> { ["xmltype"] = "jsonb" });
        var mapper = new OracleToPostgresTypeMapper(overrides);
        var result = mapper.Map(new SourceDataType("XMLTYPE", null, null, null, false));
        Assert.Equal("jsonb", result.SqlType);
    }
}

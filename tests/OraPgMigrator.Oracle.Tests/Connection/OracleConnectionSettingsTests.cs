using OraPgMigrator.Oracle.Connection;

namespace OraPgMigrator.Oracle.Tests.Connection;

public class OracleConnectionSettingsTests
{
    private static OracleConnectionSettings Base() => new()
    {
        Host = "oracle.internal",
        Port = 1521,
        Username = "migration_user",
        Password = "secret",
        Sid = "ORCL"
    };

    [Fact]
    public void Validate_WithSidOnly_Succeeds() => Base().Validate();

    [Fact]
    public void Validate_WithServiceNameOnly_Succeeds()
    {
        var settings = Base() with { Sid = null, ServiceName = "PRODDB" };
        settings.Validate();
    }

    [Fact]
    public void Validate_WithBothSidAndServiceName_Throws()
    {
        var settings = Base() with { ServiceName = "PRODDB" };
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_WithNeitherSidNorServiceName_Throws()
    {
        var settings = Base() with { Sid = null };
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_InvalidPort_Throws(int port)
    {
        var settings = Base() with { Port = port };
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void ResolvedSchema_DefaultsToUppercasedUsername_WhenSchemaNotSet()
    {
        var settings = Base();
        Assert.Equal("MIGRATION_USER", settings.ResolvedSchema);
    }

    [Fact]
    public void ResolvedSchema_UsesExplicitSchema_WhenProvided()
    {
        var settings = Base() with { Schema = "legacy_app" };
        Assert.Equal("LEGACY_APP", settings.ResolvedSchema);
    }

    [Fact]
    public void ResolvedSchema_PreservesQuotedCaseSensitiveSchema()
    {
        var settings = Base() with { Schema = "\"LegacyApp\"" };
        Assert.Equal("LegacyApp", settings.ResolvedSchema);
    }

    [Fact]
    public void ToString_NeverContainsThePassword()
    {
        var settings = Base() with { Password = "TopSecret123" };
        Assert.DoesNotContain("TopSecret123", settings.ToString());
    }

    [Fact]
    public void ToString_MasksPasswordWithAsterisks()
    {
        var settings = Base();
        Assert.Contains("*****", settings.ToString());
    }
}

using OraPgMigrator.Oracle;

namespace OraPgMigrator.Oracle.Tests;

public class OracleIdentifiersTests
{
    [Fact]
    public void Normalize_UnquotedLowercase_BecomesUppercase() =>
        Assert.Equal("LEGACY_APP", OracleIdentifiers.Normalize("legacy_app"));

    [Fact]
    public void Normalize_AlreadyUppercase_StaysUppercase() =>
        Assert.Equal("LEGACY_APP", OracleIdentifiers.Normalize("LEGACY_APP"));

    [Fact]
    public void Normalize_QuotedIdentifier_PreservesCasingAndStripsQuotes() =>
        Assert.Equal("LegacyApp", OracleIdentifiers.Normalize("\"LegacyApp\""));

    [Fact]
    public void Normalize_TrimsWhitespace() =>
        Assert.Equal("LEGACY", OracleIdentifiers.Normalize("  legacy  "));
}

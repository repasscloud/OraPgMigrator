using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Tests.Validation;

public class PostgresIdentifiersTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("USER")]
    [InlineData("order")]
    [InlineData("group")]
    [InlineData("position")]
    [InlineData("current_user")]
    public void IsReserved_DetectsReservedWords(string word) => Assert.True(PostgresIdentifiers.IsReserved(word));

    [Fact]
    public void IsReserved_DoesNotFlagOrdinaryWord() => Assert.False(PostgresIdentifiers.IsReserved("customer"));

    [Fact]
    public void Normalize_Lower_LowercasesIdentifier() =>
        Assert.Equal("customer_address", PostgresIdentifiers.Normalize("CUSTOMER_ADDRESS", IdentifierCase.Lower));

    [Fact]
    public void Normalize_Preserve_KeepsOriginalCasing() =>
        Assert.Equal("CUSTOMER_ADDRESS", PostgresIdentifiers.Normalize("CUSTOMER_ADDRESS", IdentifierCase.Preserve));

    [Fact]
    public void Quote_ReservedWord_IsQuoted() =>
        Assert.Equal("\"order\"", PostgresIdentifiers.Quote("order", IdentifierCase.Lower));

    [Fact]
    public void Quote_OrdinaryLowerIdentifier_IsNotQuoted() =>
        Assert.Equal("customer", PostgresIdentifiers.Quote("customer", IdentifierCase.Lower));

    [Fact]
    public void Quote_PreserveMixedCase_IsQuoted() =>
        Assert.Equal("\"Customer\"", PostgresIdentifiers.Quote("Customer", IdentifierCase.Preserve));

    [Fact]
    public void IsValidUnquotedIdentifier_RejectsLeadingDigit() =>
        Assert.False(PostgresIdentifiers.IsValidUnquotedIdentifier("1abc"));

    [Fact]
    public void IsValidUnquotedIdentifier_AcceptsUnderscoreAndDigits() =>
        Assert.True(PostgresIdentifiers.IsValidUnquotedIdentifier("customer_id_2"));
}

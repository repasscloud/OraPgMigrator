namespace OraPgMigrator.Postgres.Validation;

public enum IdentifierCase
{
    /// <summary>Normalize to lowercase (PostgreSQL's convenient, unquoted default). This is the application default.</summary>
    Lower,

    /// <summary>Preserve the source identifier's casing as-is (requires quoting in generated DDL when mixed-case).</summary>
    Preserve
}

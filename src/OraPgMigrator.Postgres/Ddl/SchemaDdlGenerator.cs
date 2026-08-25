using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Ddl;

public sealed class SchemaDdlGenerator
{
    private readonly IdentifierCase _identifierCase;

    public SchemaDdlGenerator(IdentifierCase identifierCase)
    {
        _identifierCase = identifierCase;
    }

    public DdlGenerationResult Generate(string targetSchema)
    {
        var name = PostgresIdentifiers.Normalize(targetSchema, _identifierCase);
        var quoted = PostgresIdentifiers.Quote(name, _identifierCase);
        return new DdlGenerationResult(name, $"CREATE SCHEMA IF NOT EXISTS {quoted};{Environment.NewLine}", []);
    }
}

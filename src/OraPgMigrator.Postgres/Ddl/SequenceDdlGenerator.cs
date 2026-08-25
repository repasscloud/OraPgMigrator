using OraPgMigrator.Core.Models;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Ddl;

/// <summary>
/// Generates CREATE SEQUENCE DDL. The start value is always the sequence's next
/// value at scan time, so a migrated sequence never starts behind already-exported
/// table data (spec §21).
/// </summary>
public sealed class SequenceDdlGenerator
{
    private readonly IdentifierCase _identifierCase;

    public SequenceDdlGenerator(IdentifierCase identifierCase)
    {
        _identifierCase = identifierCase;
    }

    public DdlGenerationResult Generate(SequenceDefinition sequence, string targetSchema)
    {
        var name = PostgresIdentifiers.Normalize(sequence.Name, _identifierCase);
        var quotedName = PostgresIdentifiers.Quote(name, _identifierCase);
        var quotedSchema = PostgresIdentifiers.Quote(PostgresIdentifiers.Normalize(targetSchema, _identifierCase), _identifierCase);

        var sql = $"""
            CREATE SEQUENCE {quotedSchema}.{quotedName}
                START WITH {sequence.StartValue}
                INCREMENT BY {sequence.IncrementBy}{(sequence.MinValue is { } min ? $"{Environment.NewLine}    MINVALUE {min}" : string.Empty)}{(sequence.MaxValue is { } max ? $"{Environment.NewLine}    MAXVALUE {max}" : string.Empty)}{(sequence.CacheSize > 1 ? $"{Environment.NewLine}    CACHE {sequence.CacheSize}" : string.Empty)}{(sequence.Cycle ? $"{Environment.NewLine}    CYCLE" : string.Empty)};

            """;

        return new DdlGenerationResult(name, sql, []);
    }
}

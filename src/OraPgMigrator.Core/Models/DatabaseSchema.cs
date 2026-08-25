namespace OraPgMigrator.Core.Models;

/// <summary>
/// Full snapshot of a scanned source schema. This is the neutral, source-agnostic
/// model persisted to metadata.json so later stages (ddl, export) never need to
/// reconnect to the source database purely for schema information.
/// </summary>
public sealed record DatabaseSchema(
    string SourceDatabase,
    string SchemaName,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<DatabaseTable> Tables,
    IReadOnlyList<SequenceDefinition> Sequences,
    IReadOnlyList<ViewDefinition> Views,
    IReadOnlyList<SynonymDefinition> Synonyms,
    IReadOnlyList<RoutineDefinition> Routines);

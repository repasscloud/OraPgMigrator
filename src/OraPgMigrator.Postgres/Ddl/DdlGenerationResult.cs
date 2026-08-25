using OraPgMigrator.Core.Validation;

namespace OraPgMigrator.Postgres.Ddl;

/// <summary>
/// Result of generating one DDL artifact (a table, a PK/FK/index/sequence file).
/// <see cref="Sql"/> is null when generation failed — callers must not write a
/// partial/incorrect file in that case, only surface <see cref="Issues"/>.
/// </summary>
public sealed record DdlGenerationResult(string FileBaseName, string? Sql, IReadOnlyList<ValidationIssue> Issues)
{
    public bool Success => Sql is not null;
}

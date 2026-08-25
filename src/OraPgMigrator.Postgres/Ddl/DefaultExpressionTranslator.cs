using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Postgres.Ddl;

public sealed record TranslatedDefault(string? Expression, string? Warning);

/// <summary>
/// Translates a classified Oracle column default (see <see cref="DefaultKind"/>)
/// into a PostgreSQL DEFAULT expression. Unrecognized expressions are never
/// emitted as-is into generated DDL; a warning is returned instead so the caller
/// can surface it in report.txt rather than silently producing possibly-invalid
/// SQL.
/// </summary>
public static class DefaultExpressionTranslator
{
    public static TranslatedDefault Translate(ColumnDefault? sourceDefault, string identifierCaseAdjustedTargetSequenceName)
    {
        if (sourceDefault is null)
        {
            return new TranslatedDefault(null, null);
        }

        return sourceDefault.Kind switch
        {
            DefaultKind.CurrentTimestamp => new TranslatedDefault("CURRENT_TIMESTAMP", null),
            DefaultKind.Literal => new TranslatedDefault(sourceDefault.RawExpression, null),
            DefaultKind.SequenceNextVal => new TranslatedDefault(
                $"nextval('{identifierCaseAdjustedTargetSequenceName}')", null),
            DefaultKind.Unrecognized => new TranslatedDefault(
                null, $"Default expression '{sourceDefault.RawExpression}' has no safe automatic translation; add it manually."),
            _ => new TranslatedDefault(null, null)
        };
    }
}

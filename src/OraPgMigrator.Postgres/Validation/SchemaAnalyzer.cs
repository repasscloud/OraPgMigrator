using OraPgMigrator.Core.Mapping;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Core.Validation;

namespace OraPgMigrator.Postgres.Validation;

/// <summary>
/// Analyzes a scanned schema against the PostgreSQL target and produces
/// <see cref="ValidationIssue"/>s for report.txt — unsupported types, reserved
/// identifiers, post-normalization collisions, missing precision/scale, etc.
/// (spec §11). Analysis never mutates or fails the scan; it only surfaces risks.
/// </summary>
public static class SchemaAnalyzer
{
    public static IReadOnlyList<ValidationIssue> Analyze(DatabaseSchema schema, IDataTypeMapper typeMapper, IdentifierCase identifierCase)
    {
        var issues = new List<ValidationIssue>();
        var seenTargetTableNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var noExplicitPrecisionCount = 0;

        foreach (var table in schema.Tables)
        {
            var targetTableName = PostgresIdentifiers.Normalize(table.Name, identifierCase);
            if (seenTargetTableNames.TryGetValue(targetTableName, out var existing) && !string.Equals(existing, table.Name, StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "IdentifierCollision", $"Tables '{existing}' and '{table.Name}' both normalize to '{targetTableName}'."));
            }
            else
            {
                seenTargetTableNames[targetTableName] = table.Name;
            }

            if (PostgresIdentifiers.IsReserved(targetTableName))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ReservedIdentifier", $"Table '{table.Name}' normalizes to reserved word '{targetTableName}' and will be quoted."));
            }

            var seenTargetColumnNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in table.Columns)
            {
                var targetColumnName = PostgresIdentifiers.Normalize(column.Name, identifierCase);
                if (seenTargetColumnNames.TryGetValue(targetColumnName, out var existingCol) && !string.Equals(existingCol, column.Name, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "IdentifierCollision", $"{table.Name}: columns '{existingCol}' and '{column.Name}' both normalize to '{targetColumnName}'."));
                }
                else
                {
                    seenTargetColumnNames[targetColumnName] = column.Name;
                }

                if (PostgresIdentifiers.IsReserved(targetColumnName))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ReservedIdentifier", $"{table.Name}.{column.Name} normalizes to reserved word '{targetColumnName}' and will be quoted."));
                }

                if (targetColumnName.Length > PostgresIdentifiers.MaxLength)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "IdentifierTooLong", $"{table.Name}.{column.Name}: normalized identifier '{targetColumnName}' exceeds PostgreSQL's {PostgresIdentifiers.MaxLength}-character limit."));
                }

                if (string.Equals(column.SourceType.NativeTypeName, "NUMBER", StringComparison.OrdinalIgnoreCase) && column.SourceType.Precision is null)
                {
                    noExplicitPrecisionCount++;
                }

                try
                {
                    typeMapper.Map(column.SourceType);
                }
                catch (UnsupportedSourceTypeException ex)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "UnsupportedType", $"{table.Name}.{column.Name}: unsupported source type '{ex.NativeTypeName}'."));
                }
            }

            if (targetTableName.Length > PostgresIdentifiers.MaxLength)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "IdentifierTooLong", $"Table '{table.Name}': normalized identifier '{targetTableName}' exceeds PostgreSQL's {PostgresIdentifiers.MaxLength}-character limit."));
            }

            foreach (var index in table.Indexes.Where(i => i.Kind != IndexKind.Normal))
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "UnsupportedIndex", $"{table.Name}: index '{index.Name}' is Oracle {index.Kind} and requires manual review."));
            }

            if (table.Partitioning is not null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Partitioning", $"{table.Name}: Oracle table is partitioned ({table.Partitioning.PartitioningType}); PostgreSQL partitioning was not generated automatically."));
            }

            if (table.Triggers.Count > 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Triggers", $"{table.Name}: {table.Triggers.Count} trigger(s) detected; trigger logic requires manual migration."));
            }
        }

        if (noExplicitPrecisionCount > 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "NumberPrecision", $"NUMBER columns without explicit precision/scale: {noExplicitPrecisionCount}"));
        }

        if (schema.Views.Count > 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Views", $"{schema.Views.Count} view(s) detected; view definitions were not translated."));
        }

        if (schema.Routines.Count > 0)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Routines", $"{schema.Routines.Count} stored procedure/function/package object(s) detected; PL/SQL logic requires manual migration."));
        }

        return issues;
    }
}

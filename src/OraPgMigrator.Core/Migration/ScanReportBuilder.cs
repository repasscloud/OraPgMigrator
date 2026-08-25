using System.Globalization;
using System.Text;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Core.Validation;

namespace OraPgMigrator.Core.Migration;

/// <summary>Builds the human-readable report.txt from a scanned schema plus any issues surfaced during analysis.</summary>
public static class ScanReportBuilder
{
    public static string Build(DatabaseSchema schema, IReadOnlyList<ValidationIssue> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Oracle to PostgreSQL Migration Analysis");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Source schema: {schema.SchemaName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Scanned at (UTC): {schema.ScannedAtUtc:O}");
        sb.AppendLine();

        var tables = schema.Tables;
        var totalRows = tables.Sum(t => t.RowCount);
        var totalColumns = tables.Sum(t => t.Columns.Count);
        var totalPks = tables.Count(t => t.PrimaryKey is not null);
        var totalFks = tables.Sum(t => t.ForeignKeys.Count);
        var totalIndexes = tables.Sum(t => t.Indexes.Count);

        sb.AppendLine(CultureInfo.InvariantCulture, $"Tables:                 {tables.Count,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Rows:                   {totalRows,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Columns:                {totalColumns,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Primary keys:           {totalPks,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Foreign keys:           {totalFks,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Indexes:                {totalIndexes,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Sequences:              {schema.Sequences.Count,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Views:                  {schema.Views.Count,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Synonyms:               {schema.Synonyms.Count,10:N0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Routines (proc/func/pkg):{schema.Routines.Count,9:N0}");
        sb.AppendLine();

        var noPk = tables.Where(t => t.PrimaryKey is null).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        sb.AppendLine("Tables without primary keys:");
        if (noPk.Count == 0)
        {
            sb.AppendLine("    (none)");
        }
        else
        {
            foreach (var name in noPk)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {name}");
            }
        }
        sb.AppendLine();

        if (schema.Views.Count > 0)
        {
            sb.AppendLine("Views (reported only, not migrated):");
            foreach (var v in schema.Views.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {v}");
            }
            sb.AppendLine();
        }

        if (schema.Routines.Count > 0)
        {
            sb.AppendLine("Stored procedures/functions/packages (reported only, not migrated):");
            foreach (var r in schema.Routines.OrderBy(r => r.Name, StringComparer.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {r.Name} ({r.Kind})");
            }
            sb.AppendLine();
        }

        var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
        var warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();

        sb.AppendLine("Unsupported or special source types / errors:");
        if (errors.Count == 0)
        {
            sb.AppendLine("    (none)");
        }
        else
        {
            foreach (var issue in errors)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [{issue.Category}] {issue.Message}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("Potential migration issues:");
        if (warnings.Count == 0)
        {
            sb.AppendLine("    (none)");
        }
        else
        {
            foreach (var issue in warnings)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [{issue.Category}] {issue.Message}");
            }
        }

        return sb.ToString();
    }
}

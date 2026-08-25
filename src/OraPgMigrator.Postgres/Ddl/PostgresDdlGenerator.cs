using System.Globalization;
using System.Text;
using OraPgMigrator.Core.Mapping;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Core.Validation;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Ddl;

/// <summary>
/// Generates deterministic PostgreSQL DDL for a single scanned table: the
/// CREATE TABLE statement, plus (when requested) separate primary-key,
/// foreign-key and index statements, per spec §17-§20. Foreign keys and indexes
/// are always emitted as separate artifacts from the table itself so they can be
/// applied after data load (spec §19).
/// </summary>
public sealed class PostgresDdlGenerator
{
    private readonly IDataTypeMapper _typeMapper;
    private readonly IdentifierCase _identifierCase;

    public PostgresDdlGenerator(IDataTypeMapper typeMapper, IdentifierCase identifierCase)
    {
        _typeMapper = typeMapper;
        _identifierCase = identifierCase;
    }

    public string Quote(string identifier) => PostgresIdentifiers.Quote(NormalizeCase(identifier), _identifierCase);

    public string NormalizeCase(string identifier) => PostgresIdentifiers.Normalize(identifier, _identifierCase);

    public DdlGenerationResult GenerateTable(DatabaseTable table, string targetSchema, string targetTable)
    {
        var issues = new List<ValidationIssue>();
        var columnLines = new List<string>();

        foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            MappedColumnType mapped;
            try
            {
                mapped = _typeMapper.Map(column.SourceType);
            }
            catch (UnsupportedSourceTypeException ex)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "UnsupportedType",
                    $"{table.Schema}.{table.Name}.{column.Name}: unsupported source type '{ex.NativeTypeName}'. Provide a custom type mapping or exclude this table."));
                continue;
            }

            if (mapped.Note is not null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "TypeMappingNote", $"{table.Schema}.{table.Name}.{column.Name}: {mapped.Note}"));
            }

            var translatedDefault = DefaultExpressionTranslator.Translate(
                column.Default,
                column.Default?.SequenceName is { } seq ? Quote(seq) : string.Empty);
            if (translatedDefault.Warning is not null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "UnsupportedDefault", $"{table.Schema}.{table.Name}.{column.Name}: {translatedDefault.Warning}"));
            }

            var line = new StringBuilder("    ").Append(Quote(column.Name)).Append(' ').Append(mapped.SqlType);
            if (!column.Nullable)
            {
                line.Append(" NOT NULL");
            }
            if (translatedDefault.Expression is not null)
            {
                line.Append(" DEFAULT ").Append(translatedDefault.Expression);
            }
            columnLines.Add(line.ToString());
        }

        if (issues.Any(i => i.Severity == ValidationSeverity.Error))
        {
            // Never generate DDL for a table with an unresolved column — that would silently
            // drop data-bearing columns from the target schema.
            return new DdlGenerationResult(FileBaseNameFor(targetTable), null, issues);
        }

        var sql = new StringBuilder();
        sql.Append("CREATE TABLE ").Append(Quote(targetSchema)).Append('.').Append(Quote(targetTable)).AppendLine();
        sql.AppendLine("(");
        sql.Append(string.Join("," + Environment.NewLine, columnLines));
        sql.AppendLine();
        sql.AppendLine(");");

        if (table.Comment is not null)
        {
            sql.Append("COMMENT ON TABLE ").Append(Quote(targetSchema)).Append('.').Append(Quote(targetTable))
                .Append(" IS ").Append(QuoteLiteral(table.Comment)).AppendLine(";");
        }

        return new DdlGenerationResult(FileBaseNameFor(targetTable), sql.ToString(), issues);
    }

    public DdlGenerationResult GeneratePrimaryKey(DatabaseTable table, string targetSchema, string targetTable)
    {
        if (table.PrimaryKey is null)
        {
            return new DdlGenerationResult(FileBaseNameFor(targetTable), null, [
                new ValidationIssue(ValidationSeverity.Warning, "MissingPrimaryKey", $"{table.Schema}.{table.Name} has no primary key; skipping primary key DDL.")
            ]);
        }

        var columns = string.Join(", ", table.PrimaryKey.Columns.Select(Quote));
        var sql = $"ALTER TABLE {Quote(targetSchema)}.{Quote(targetTable)} ADD CONSTRAINT {Quote(NormalizeCase(table.PrimaryKey.Name))} PRIMARY KEY ({columns});{Environment.NewLine}";
        return new DdlGenerationResult(FileBaseNameFor(targetTable), sql, []);
    }

    public DdlGenerationResult GenerateForeignKeys(DatabaseTable table, string targetSchema, string targetTable, Func<string, string, (string Schema, string Table)> resolveTarget)
    {
        if (table.ForeignKeys.Count == 0)
        {
            return new DdlGenerationResult(FileBaseNameFor(targetTable), null, []);
        }

        var sql = new StringBuilder();
        foreach (var fk in table.ForeignKeys)
        {
            var (refSchema, refTable) = resolveTarget(fk.ReferencedSchema, fk.ReferencedTable);
            var columns = string.Join(", ", fk.Columns.Select(Quote));
            var refColumns = string.Join(", ", fk.ReferencedColumns.Select(Quote));
            sql.Append("ALTER TABLE ").Append(Quote(targetSchema)).Append('.').Append(Quote(targetTable))
                .Append(" ADD CONSTRAINT ").Append(Quote(NormalizeCase(fk.Name)))
                .Append(" FOREIGN KEY (").Append(columns).Append(") REFERENCES ")
                .Append(Quote(refSchema)).Append('.').Append(Quote(refTable))
                .Append(" (").Append(refColumns).Append(");").AppendLine();
        }

        return new DdlGenerationResult(FileBaseNameFor(targetTable), sql.ToString(), []);
    }

    public DdlGenerationResult GenerateIndexes(DatabaseTable table, string targetSchema, string targetTable)
    {
        var issues = new List<ValidationIssue>();
        var sql = new StringBuilder();

        foreach (var index in table.Indexes)
        {
            if (index.Kind != IndexKind.Normal)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "UnsupportedIndex",
                    $"{table.Schema}.{table.Name}: index '{index.Name}' is Oracle {index.Kind} and was not translated automatically."));
                continue;
            }

            var columns = string.Join(", ", index.Columns.Select(Quote));
            var unique = index.IsUnique ? "UNIQUE " : string.Empty;
            sql.Append("CREATE ").Append(unique).Append("INDEX ").Append(Quote(NormalizeCase(index.Name)))
                .Append(" ON ").Append(Quote(targetSchema)).Append('.').Append(Quote(targetTable))
                .Append(" (").Append(columns).Append(");").AppendLine();
        }

        return new DdlGenerationResult(FileBaseNameFor(targetTable), sql.Length > 0 ? sql.ToString() : null, issues);
    }

    private static string FileBaseNameFor(string targetTable) => targetTable.ToLowerInvariant();

    private static string QuoteLiteral(string value) => $"'{value.Replace("'", "''")}'";
}

using System.Collections.Concurrent;
using OraPgMigrator.Core.Abstractions;
using OraPgMigrator.Core.Models;
using OraPgMigrator.Oracle.Connection;
using OracleClient = global::Oracle.ManagedDataAccess.Client;

namespace OraPgMigrator.Oracle.SchemaReader;

/// <summary>
/// Reads Oracle data-dictionary metadata (ALL_* views, scoped to the requested
/// schema) into the neutral <see cref="DatabaseSchema"/> model. Bounded
/// concurrency is used for the row-counting phase since that is the only part of
/// a scan whose cost scales with table size.
/// </summary>
public sealed class OracleSchemaReader : ISourceSchemaReader
{
    private const int MaxConcurrentRowCounts = 4;

    private readonly OracleConnectionSettings _settings;

    public OracleSchemaReader(OracleConnectionSettings settings)
    {
        _settings = settings;
    }

    public async Task<bool> SchemaExistsAsync(string schemaName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME = :owner";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", schemaName));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return System.Convert.ToInt32(result) > 0;
    }

    public async Task<DatabaseSchema> ScanSchemaAsync(string schemaName, RowCountMode rowCountMode, CancellationToken cancellationToken)
    {
        var owner = OracleIdentifiers.Normalize(schemaName);

        await using var connection = await OpenAsync(cancellationToken);

        var databaseName = await ReadDatabaseNameAsync(connection, cancellationToken);
        var tableNames = await ReadTableNamesAsync(connection, owner, cancellationToken);
        var columnsByTable = await ReadColumnsAsync(connection, owner, cancellationToken);
        var commentsByTable = await ReadTableCommentsAsync(connection, owner, cancellationToken);
        var columnCommentsByTable = await ReadColumnCommentsAsync(connection, owner, cancellationToken);
        ApplyColumnComments(columnsByTable, columnCommentsByTable);
        var primaryKeys = await ReadPrimaryKeysAsync(connection, owner, cancellationToken);
        var uniqueConstraints = await ReadUniqueConstraintsAsync(connection, owner, cancellationToken);
        var checkConstraints = await ReadCheckConstraintsAsync(connection, owner, cancellationToken);
        var foreignKeys = await ReadForeignKeysAsync(connection, owner, cancellationToken);
        var indexes = await ReadIndexesAsync(connection, owner, cancellationToken);
        var triggers = await ReadTriggersAsync(connection, owner, cancellationToken);
        var partitioning = await ReadPartitioningAsync(connection, owner, cancellationToken);
        var sequences = await ReadSequencesAsync(connection, owner, cancellationToken);
        var views = await ReadViewsAsync(connection, owner, cancellationToken);
        var synonyms = await ReadSynonymsAsync(connection, owner, cancellationToken);
        var routines = await ReadRoutinesAsync(connection, owner, cancellationToken);

        var rowCounts = rowCountMode == RowCountMode.Exact
            ? await ReadExactRowCountsAsync(owner, tableNames, cancellationToken)
            : await ReadStatisticsRowCountsAsync(connection, owner, tableNames, cancellationToken);

        var tables = tableNames
            .OrderBy(t => t, StringComparer.Ordinal)
            .Select(name => new DatabaseTable(
                owner,
                name,
                rowCounts.GetValueOrDefault(name, 0),
                columnsByTable.GetValueOrDefault(name, []),
                primaryKeys.GetValueOrDefault(name),
                foreignKeys.GetValueOrDefault(name, []),
                uniqueConstraints.GetValueOrDefault(name, []),
                checkConstraints.GetValueOrDefault(name, []),
                indexes.GetValueOrDefault(name, []),
                triggers.GetValueOrDefault(name, []),
                partitioning.GetValueOrDefault(name),
                commentsByTable.GetValueOrDefault(name)))
            .ToList();

        return new DatabaseSchema(
            databaseName,
            owner,
            DateTimeOffset.UtcNow,
            tables,
            sequences,
            views,
            synonyms,
            routines);
    }

    private static void ApplyColumnComments(
        Dictionary<string, List<DatabaseColumn>> columnsByTable,
        Dictionary<string, Dictionary<string, string?>> columnCommentsByTable)
    {
        foreach (var (table, columns) in columnsByTable)
        {
            if (!columnCommentsByTable.TryGetValue(table, out var comments))
            {
                continue;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                if (comments.TryGetValue(columns[i].Name, out var comment) && comment is not null)
                {
                    columns[i] = columns[i] with { Comment = comment };
                }
            }
        }
    }

    private async Task<OracleClient.OracleConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new OracleClient.OracleConnection(OracleConnectionStringFactory.Build(_settings));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<string> ReadDatabaseNameAsync(OracleClient.OracleConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ORA_DATABASE_NAME FROM DUAL";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString() ?? "ORACLE";
    }

    private static async Task<List<string>> ReadTableNamesAsync(OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var names = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER = :owner ORDER BY TABLE_NAME";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    private static async Task<Dictionary<string, List<DatabaseColumn>>> ReadColumnsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<DatabaseColumn>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, DATA_LENGTH, CHAR_LENGTH, CHAR_USED,
                   DATA_PRECISION, DATA_SCALE, NULLABLE, DATA_DEFAULT, COLUMN_ID
            FROM ALL_TAB_COLUMNS
            WHERE OWNER = :owner
            ORDER BY TABLE_NAME, COLUMN_ID
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var name = reader.GetString(1);
            var dataType = reader.GetString(2);
            int? dataLength = reader.IsDBNull(3) ? null : System.Convert.ToInt32(reader.GetValue(3));
            int? charLength = reader.IsDBNull(4) ? null : System.Convert.ToInt32(reader.GetValue(4));
            var charUsed = !reader.IsDBNull(5) && reader.GetString(5) == "C";
            int? precision = reader.IsDBNull(6) ? null : System.Convert.ToInt32(reader.GetValue(6));
            int? scale = reader.IsDBNull(7) ? null : System.Convert.ToInt32(reader.GetValue(7));
            var nullable = reader.IsDBNull(8) || reader.GetString(8) == "Y";
            var defaultExpr = reader.IsDBNull(9) ? null : reader.GetString(9).Trim();
            var length = charUsed ? charLength : dataLength;

            var sourceType = new SourceDataType(dataType, length, precision, scale, charUsed);
            var defaultValue = ClassifyDefault(defaultExpr);
            var column = new DatabaseColumn(name, reader.GetInt32(10), sourceType, nullable, defaultValue, null);

            if (!result.TryGetValue(table, out var list))
            {
                list = [];
                result[table] = list;
            }
            list.Add(column);
        }
        return result;
    }

    private static ColumnDefault? ClassifyDefault(string? rawExpression)
    {
        if (string.IsNullOrWhiteSpace(rawExpression))
        {
            return null;
        }

        var trimmed = rawExpression.Trim();
        var upper = trimmed.TrimEnd(';').ToUpperInvariant();

        if (upper is "SYSDATE" or "SYSTIMESTAMP" or "CURRENT_TIMESTAMP" or "CURRENT_DATE")
        {
            return new ColumnDefault(trimmed, DefaultKind.CurrentTimestamp);
        }

        if (upper.EndsWith(".NEXTVAL", StringComparison.Ordinal))
        {
            var sequenceName = upper[..^".NEXTVAL".Length];
            var dot = sequenceName.LastIndexOf('.');
            if (dot >= 0)
            {
                sequenceName = sequenceName[(dot + 1)..];
            }
            return new ColumnDefault(trimmed, DefaultKind.SequenceNextVal, sequenceName);
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^'.*'$") ||
            System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^-?\d+(\.\d+)?$"))
        {
            return new ColumnDefault(trimmed, DefaultKind.Literal);
        }

        return new ColumnDefault(trimmed, DefaultKind.Unrecognized);
    }

    private static async Task<Dictionary<string, string?>> ReadTableCommentsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME, COMMENTS FROM ALL_TAB_COMMENTS WHERE OWNER = :owner AND COMMENTS IS NOT NULL";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        return result;
    }

    private static async Task<Dictionary<string, Dictionary<string, string?>>> ReadColumnCommentsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME, COLUMN_NAME, COMMENTS FROM ALL_COL_COMMENTS WHERE OWNER = :owner AND COMMENTS IS NOT NULL";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (!result.TryGetValue(table, out var columns))
            {
                columns = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                result[table] = columns;
            }
            columns[reader.GetString(1)] = reader.IsDBNull(2) ? null : reader.GetString(2);
        }
        return result;
    }

    private static async Task<Dictionary<string, PrimaryKeyDefinition>> ReadPrimaryKeysAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var byTable = new Dictionary<string, (string ConstraintName, List<string> Columns)>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.TABLE_NAME, c.CONSTRAINT_NAME, cc.COLUMN_NAME
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc
              ON cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
            WHERE c.OWNER = :owner AND c.CONSTRAINT_TYPE = 'P'
            ORDER BY c.TABLE_NAME, cc.POSITION
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var constraintName = reader.GetString(1);
            var column = reader.GetString(2);
            if (!byTable.TryGetValue(table, out var entry))
            {
                entry = (constraintName, []);
                byTable[table] = entry;
            }
            entry.Columns.Add(column);
        }
        return byTable.ToDictionary(kv => kv.Key, kv => new PrimaryKeyDefinition(kv.Value.ConstraintName, kv.Value.Columns), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, List<UniqueConstraintDefinition>>> ReadUniqueConstraintsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var raw = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.TABLE_NAME, c.CONSTRAINT_NAME, cc.COLUMN_NAME
            FROM ALL_CONSTRAINTS c
            JOIN ALL_CONS_COLUMNS cc
              ON cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
            WHERE c.OWNER = :owner AND c.CONSTRAINT_TYPE = 'U'
            ORDER BY c.TABLE_NAME, c.CONSTRAINT_NAME, cc.POSITION
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var constraintName = reader.GetString(1);
            var column = reader.GetString(2);
            if (!raw.TryGetValue(table, out var byConstraint))
            {
                byConstraint = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                raw[table] = byConstraint;
            }
            if (!byConstraint.TryGetValue(constraintName, out var cols))
            {
                cols = [];
                byConstraint[constraintName] = cols;
            }
            cols.Add(column);
        }
        return raw.ToDictionary(
            t => t.Key,
            t => t.Value.Select(c => new UniqueConstraintDefinition(c.Key, c.Value)).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, List<CheckConstraintDefinition>>> ReadCheckConstraintsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<CheckConstraintDefinition>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        // Exclude system-generated NOT NULL checks (SEARCH_CONDITION IS NOT NULL naming convention starts with SYS_).
        command.CommandText = """
            SELECT TABLE_NAME, CONSTRAINT_NAME, SEARCH_CONDITION
            FROM ALL_CONSTRAINTS
            WHERE OWNER = :owner AND CONSTRAINT_TYPE = 'C' AND GENERATED = 'USER NAME'
            ORDER BY TABLE_NAME, CONSTRAINT_NAME
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (!result.TryGetValue(table, out var list))
            {
                list = [];
                result[table] = list;
            }
            list.Add(new CheckConstraintDefinition(reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }
        return result;
    }

    private static async Task<Dictionary<string, List<ForeignKeyDefinition>>> ReadForeignKeysAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<(string Name, List<string> Columns, string RefOwner, string RefTable, List<string> RefColumns)>>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                fk.TABLE_NAME, fk.CONSTRAINT_NAME, fkc.COLUMN_NAME, fkc.POSITION,
                pk.OWNER AS PK_OWNER, pk.TABLE_NAME AS PK_TABLE, pkc.COLUMN_NAME AS PK_COLUMN
            FROM ALL_CONSTRAINTS fk
            JOIN ALL_CONS_COLUMNS fkc
              ON fkc.OWNER = fk.OWNER AND fkc.CONSTRAINT_NAME = fk.CONSTRAINT_NAME
            JOIN ALL_CONSTRAINTS pk
              ON pk.OWNER = fk.R_OWNER AND pk.CONSTRAINT_NAME = fk.R_CONSTRAINT_NAME
            JOIN ALL_CONS_COLUMNS pkc
              ON pkc.OWNER = pk.OWNER AND pkc.CONSTRAINT_NAME = pk.CONSTRAINT_NAME AND pkc.POSITION = fkc.POSITION
            WHERE fk.OWNER = :owner AND fk.CONSTRAINT_TYPE = 'R'
            ORDER BY fk.TABLE_NAME, fk.CONSTRAINT_NAME, fkc.POSITION
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var byConstraint = new Dictionary<string, (string Table, string Name, List<string> Columns, string RefOwner, string RefTable, List<string> RefColumns)>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            var constraintName = reader.GetString(1);
            var key = $"{table}.{constraintName}";
            if (!byConstraint.TryGetValue(key, out var entry))
            {
                entry = (table, constraintName, [], reader.GetString(4), reader.GetString(5), []);
                byConstraint[key] = entry;
            }
            entry.Columns.Add(reader.GetString(2));
            entry.RefColumns.Add(reader.GetString(6));
        }

        foreach (var entry in byConstraint.Values)
        {
            if (!result.TryGetValue(entry.Table, out var list))
            {
                list = [];
                result[entry.Table] = list;
            }
            list.Add((entry.Name, entry.Columns, entry.RefOwner, entry.RefTable, entry.RefColumns));
        }

        return result.ToDictionary(
            t => t.Key,
            t => t.Value.Select(e => new ForeignKeyDefinition(e.Name, e.Columns, e.RefOwner, e.RefTable, e.RefColumns)).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, List<IndexDefinition>>> ReadIndexesAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var indexMeta = new Dictionary<string, (string Table, bool Unique, string IndexType)>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT INDEX_NAME, TABLE_NAME, UNIQUENESS, INDEX_TYPE
                FROM ALL_INDEXES
                WHERE OWNER = :owner
                """;
            command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                indexMeta[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2) == "UNIQUE", reader.IsDBNull(3) ? "NORMAL" : reader.GetString(3));
            }
        }

        // Constraint-backed indexes (PK/UK) are represented via the constraint DDL, not as separate indexes.
        var constraintIndexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT INDEX_NAME FROM ALL_CONSTRAINTS WHERE OWNER = :owner AND CONSTRAINT_TYPE IN ('P','U') AND INDEX_NAME IS NOT NULL";
            command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                constraintIndexNames.Add(reader.GetString(0));
            }
        }

        var columnsByIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT INDEX_NAME, COLUMN_NAME
                FROM ALL_IND_COLUMNS
                WHERE INDEX_OWNER = :owner
                ORDER BY INDEX_NAME, COLUMN_POSITION
                """;
            command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var indexName = reader.GetString(0);
                if (!columnsByIndex.TryGetValue(indexName, out var cols))
                {
                    cols = [];
                    columnsByIndex[indexName] = cols;
                }
                cols.Add(reader.GetString(1));
            }
        }

        var result = new Dictionary<string, List<IndexDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (indexName, meta) in indexMeta)
        {
            if (constraintIndexNames.Contains(indexName))
            {
                continue;
            }

            var kind = meta.IndexType switch
            {
                "BITMAP" => IndexKind.Bitmap,
                "FUNCTION-BASED NORMAL" or "FUNCTION-BASED BITMAP" => IndexKind.FunctionBased,
                "DOMAIN" => IndexKind.Domain,
                "IOT - TOP" => IndexKind.Normal,
                _ => IndexKind.Normal
            };

            var columns = columnsByIndex.GetValueOrDefault(indexName, []);
            if (!result.TryGetValue(meta.Table, out var list))
            {
                list = [];
                result[meta.Table] = list;
            }
            list.Add(new IndexDefinition(indexName, columns, meta.Unique, kind));
        }
        return result;
    }

    private static async Task<Dictionary<string, List<TriggerDefinition>>> ReadTriggersAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<TriggerDefinition>>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TRIGGER_NAME, TABLE_NAME, TRIGGER_BODY FROM ALL_TRIGGERS WHERE OWNER = :owner AND BASE_OBJECT_TYPE = 'TABLE'";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(1);
            if (!result.TryGetValue(table, out var list))
            {
                list = [];
                result[table] = list;
            }
            list.Add(new TriggerDefinition(reader.GetString(0), table, reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }
        return result;
    }

    private static async Task<Dictionary<string, PartitioningInfo>> ReadPartitioningAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, PartitioningInfo>(StringComparer.OrdinalIgnoreCase);
        var partitionKeys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT NAME, COLUMN_NAME FROM ALL_PART_KEY_COLUMNS WHERE OWNER = :owner AND OBJECT_TYPE = 'TABLE' ORDER BY NAME, COLUMN_POSITION";
            command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.GetString(0);
                if (!partitionKeys.TryGetValue(table, out var cols))
                {
                    cols = [];
                    partitionKeys[table] = cols;
                }
                cols.Add(reader.GetString(1));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TABLE_NAME, PARTITIONING_TYPE FROM ALL_PART_TABLES WHERE OWNER = :owner";
            command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var table = reader.GetString(0);
                result[table] = new PartitioningInfo(table, reader.GetString(1), partitionKeys.GetValueOrDefault(table, []));
            }
        }

        return result;
    }

    private static async Task<List<SequenceDefinition>> ReadSequencesAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new List<SequenceDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SEQUENCE_NAME, LAST_NUMBER, INCREMENT_BY, MIN_VALUE, MAX_VALUE, CYCLE_FLAG, CACHE_SIZE
            FROM ALL_SEQUENCES
            WHERE SEQUENCE_OWNER = :owner
            ORDER BY SEQUENCE_NAME
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SequenceDefinition(
                reader.GetString(0),
                System.Convert.ToInt64(reader.GetValue(1)),
                System.Convert.ToInt64(reader.GetValue(2)),
                reader.IsDBNull(3) ? null : System.Convert.ToInt64(reader.GetValue(3)),
                reader.IsDBNull(4) ? null : System.Convert.ToInt64(reader.GetValue(4)),
                reader.GetString(5) == "Y",
                reader.IsDBNull(6) ? 0 : System.Convert.ToInt64(reader.GetValue(6))));
        }
        return result;
    }

    private static async Task<List<ViewDefinition>> ReadViewsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new List<ViewDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VIEW_NAME, TEXT FROM ALL_VIEWS WHERE OWNER = :owner ORDER BY VIEW_NAME";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ViewDefinition(reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }
        return result;
    }

    private static async Task<List<SynonymDefinition>> ReadSynonymsAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new List<SynonymDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SYNONYM_NAME, TABLE_OWNER, TABLE_NAME FROM ALL_SYNONYMS WHERE OWNER = :owner ORDER BY SYNONYM_NAME";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SynonymDefinition(reader.GetString(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }
        return result;
    }

    private static async Task<List<RoutineDefinition>> ReadRoutinesAsync(
        OracleClient.OracleConnection connection, string owner, CancellationToken cancellationToken)
    {
        var result = new List<RoutineDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT OBJECT_NAME, OBJECT_TYPE
            FROM ALL_OBJECTS
            WHERE OWNER = :owner AND OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY')
            ORDER BY OBJECT_NAME
            """;
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var kind = reader.GetString(1) switch
            {
                "PROCEDURE" => RoutineKind.Procedure,
                "FUNCTION" => RoutineKind.Function,
                "PACKAGE" => RoutineKind.Package,
                "PACKAGE BODY" => RoutineKind.PackageBody,
                _ => RoutineKind.Procedure
            };
            result.Add(new RoutineDefinition(reader.GetString(0), kind));
        }
        return result;
    }

    private async Task<Dictionary<string, long>> ReadExactRowCountsAsync(string owner, List<string> tableNames, CancellationToken cancellationToken)
    {
        var results = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var throttle = new SemaphoreSlim(MaxConcurrentRowCounts);

        var tasks = tableNames.Select(async table =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                await using var connection = await OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM \"{owner}\".\"{table}\"";
                command.CommandTimeout = 0;
                var count = await command.ExecuteScalarAsync(cancellationToken);
                results[table] = System.Convert.ToInt64(count);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return new Dictionary<string, long>(results, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, long>> ReadStatisticsRowCountsAsync(
        OracleClient.OracleConnection connection, string owner, List<string> tableNames, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME, NUM_ROWS FROM ALL_TABLES WHERE OWNER = :owner";
        command.Parameters.Add(new OracleClient.OracleParameter("owner", owner));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.IsDBNull(1) ? 0 : System.Convert.ToInt64(reader.GetValue(1));
        }

        foreach (var table in tableNames)
        {
            result.TryAdd(table, 0);
        }
        return result;
    }
}

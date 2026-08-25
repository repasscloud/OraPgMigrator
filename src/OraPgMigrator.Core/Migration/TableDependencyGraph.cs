using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Migration;

public sealed record DependencyOrderingResult(
    IReadOnlyList<string> OrderedTables,
    IReadOnlyList<string> CyclicTables);

/// <summary>
/// Builds a load ordering from foreign-key dependencies so tables are created/loaded
/// before the tables that reference them. Cycles are detected and reported rather
/// than causing failures elsewhere: foreign keys are always generated as a separate
/// stage applied after data load, so a cycle only affects suggested ordering, not
/// correctness.
/// </summary>
public static class TableDependencyGraph
{
    public static DependencyOrderingResult Order(IReadOnlyList<DatabaseTable> tables)
    {
        var byKey = tables.ToDictionary(t => Key(t.Schema, t.Name), StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            var key = Key(table.Schema, table.Name);
            edges[key] = [];
            foreach (var fk in table.ForeignKeys)
            {
                var refKey = Key(fk.ReferencedSchema, fk.ReferencedTable);
                if (byKey.ContainsKey(refKey) && !string.Equals(refKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    edges[key].Add(refKey);
                }
            }
        }

        var visited = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0=unvisited,1=visiting,2=done
        var ordered = new List<string>();
        var cyclic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in edges.Keys)
        {
            Visit(key, edges, visited, ordered, cyclic);
        }

        return new DependencyOrderingResult(ordered, [.. cyclic]);
    }

    private static void Visit(
        string key,
        Dictionary<string, HashSet<string>> edges,
        Dictionary<string, int> visited,
        List<string> ordered,
        HashSet<string> cyclic)
    {
        if (visited.TryGetValue(key, out var state))
        {
            if (state == 1)
            {
                cyclic.Add(key);
            }
            return;
        }

        visited[key] = 1;
        foreach (var dependency in edges[key])
        {
            Visit(dependency, edges, visited, ordered, cyclic);
        }

        visited[key] = 2;
        ordered.Add(key);
    }

    private static string Key(string schema, string table) => $"{schema}.{table}";
}

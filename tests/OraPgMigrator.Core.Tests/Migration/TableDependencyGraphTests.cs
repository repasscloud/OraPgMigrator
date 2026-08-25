using OraPgMigrator.Core.Migration;
using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Tests.Migration;

public class TableDependencyGraphTests
{
    private static DatabaseTable Table(string name, params ForeignKeyDefinition[] fks) =>
        new("S", name, 0, [], null, fks, [], [], [], [], null, null);

    private static ForeignKeyDefinition Fk(string toTable) => new("FK", ["COL"], "S", toTable, ["COL"]);

    [Fact]
    public void ParentBeforeChild_WhenChildReferencesParent()
    {
        var parent = Table("PARENT");
        var child = Table("CHILD", Fk("PARENT"));

        var result = TableDependencyGraph.Order([child, parent]);

        var parentIndex = result.OrderedTables.ToList().IndexOf("S.PARENT");
        var childIndex = result.OrderedTables.ToList().IndexOf("S.CHILD");
        Assert.True(parentIndex < childIndex);
        Assert.Empty(result.CyclicTables);
    }

    [Fact]
    public void Cycle_IsDetectedAndReported()
    {
        var a = Table("A", Fk("B"));
        var b = Table("B", Fk("A"));

        var result = TableDependencyGraph.Order([a, b]);

        Assert.NotEmpty(result.CyclicTables);
        Assert.Equal(2, result.OrderedTables.Count);
    }

    [Fact]
    public void TableWithNoForeignKeys_IsIncludedExactlyOnce()
    {
        var result = TableDependencyGraph.Order([Table("ONLY")]);
        Assert.Equal(["S.ONLY"], result.OrderedTables);
    }
}

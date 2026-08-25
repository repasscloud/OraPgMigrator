using OraPgMigrator.Core.Models;
using OraPgMigrator.Postgres.Ddl;
using OraPgMigrator.Postgres.Validation;

namespace OraPgMigrator.Postgres.Tests.Ddl;

public class PostgresDdlGeneratorTests
{
    private readonly PostgresDdlGenerator _generator = new(new OracleToPostgresTypeMapper(), IdentifierCase.Lower);

    private static DatabaseTable SimpleTable() => new(
        "LEGACY",
        "CUSTOMER",
        10,
        [
            new DatabaseColumn("CUSTOMER_ID", 1, new SourceDataType("NUMBER", null, 18, 0, false), false, null, null),
            new DatabaseColumn("NAME", 2, new SourceDataType("VARCHAR2", 100, null, null, true), true, null, null)
        ],
        new PrimaryKeyDefinition("PK_CUSTOMER", ["CUSTOMER_ID"]),
        [],
        [],
        [],
        [],
        [],
        null,
        null);

    [Fact]
    public void GenerateTable_ProducesCreateTableWithNotNullAndQuotedTargetNames()
    {
        var result = _generator.GenerateTable(SimpleTable(), "public", "customer");

        Assert.True(result.Success);
        Assert.Contains("CREATE TABLE public.customer", result.Sql);
        Assert.Contains("customer_id bigint NOT NULL", result.Sql);
        Assert.Contains("name varchar(100)", result.Sql);
        Assert.DoesNotContain("name varchar(100) NOT NULL", result.Sql);
    }

    [Fact]
    public void GenerateTable_IsDeterministic_AcrossRepeatedCalls()
    {
        var table = SimpleTable();
        var first = _generator.GenerateTable(table, "public", "customer");
        var second = _generator.GenerateTable(table, "public", "customer");

        Assert.Equal(first.Sql, second.Sql);
    }

    [Fact]
    public void GenerateTable_UnsupportedColumnType_FailsWithoutPartialOutput()
    {
        var table = SimpleTable() with
        {
            Columns =
            [
                new DatabaseColumn("GEOM", 1, new SourceDataType("SDO_GEOMETRY", null, null, null, false), true, null, null)
            ]
        };

        var result = _generator.GenerateTable(table, "public", "customer");

        Assert.False(result.Success);
        Assert.Null(result.Sql);
        Assert.Contains(result.Issues, i => i.Category == "UnsupportedType");
    }

    [Fact]
    public void GeneratePrimaryKey_EmitsAlterTableAddConstraint()
    {
        var result = _generator.GeneratePrimaryKey(SimpleTable(), "public", "customer");

        Assert.True(result.Success);
        Assert.Contains("ALTER TABLE public.customer ADD CONSTRAINT pk_customer PRIMARY KEY (customer_id);", result.Sql);
    }

    [Fact]
    public void GeneratePrimaryKey_NoPrimaryKey_ReturnsWarningAndNoSql()
    {
        var table = SimpleTable() with { PrimaryKey = null };
        var result = _generator.GeneratePrimaryKey(table, "public", "customer");

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Category == "MissingPrimaryKey");
    }

    [Fact]
    public void GenerateForeignKeys_ResolvesTargetViaCallback()
    {
        var table = SimpleTable() with
        {
            ForeignKeys = [new ForeignKeyDefinition("FK_ORDER_CUSTOMER", ["CUSTOMER_ID"], "LEGACY", "CUSTOMER", ["CUSTOMER_ID"])]
        };

        var result = _generator.GenerateForeignKeys(table, "public", "order_item", (_, _) => ("public", "customer"));

        Assert.True(result.Success);
        Assert.Contains("REFERENCES public.customer (customer_id)", result.Sql);
    }

    [Fact]
    public void GenerateIndexes_SkipsNonNormalIndexesWithWarning()
    {
        var table = SimpleTable() with
        {
            Indexes = [new IndexDefinition("IDX_BITMAP", ["NAME"], false, IndexKind.Bitmap)]
        };

        var result = _generator.GenerateIndexes(table, "public", "customer");

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Category == "UnsupportedIndex");
    }

    [Fact]
    public void GenerateIndexes_NormalIndex_EmitsCreateIndex()
    {
        var table = SimpleTable() with
        {
            Indexes = [new IndexDefinition("IDX_NAME", ["NAME"], false, IndexKind.Normal)]
        };

        var result = _generator.GenerateIndexes(table, "public", "customer");

        Assert.True(result.Success);
        Assert.Contains("CREATE INDEX idx_name ON public.customer (name);", result.Sql);
    }
}

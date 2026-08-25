using OraPgMigrator.Oracle.Connection;

namespace OraPgMigrator.Oracle.Tests.Connection;

public class OracleConnectionStringFactoryTests
{
    [Fact]
    public void Build_WithSid_ProducesConnectDataWithSid()
    {
        var settings = new OracleConnectionSettings
        {
            Host = "oracle.internal",
            Port = 1521,
            Username = "migration_user",
            Password = "secret",
            Sid = "ORCL"
        };

        var connectionString = OracleConnectionStringFactory.Build(settings);

        Assert.Contains("SID=ORCL", connectionString);
        Assert.Contains("HOST=oracle.internal", connectionString);
        Assert.Contains("PORT=1521", connectionString);
        Assert.Contains("secret", connectionString); // real connection string must contain the actual password
    }

    [Fact]
    public void Build_WithServiceName_ProducesConnectDataWithServiceName()
    {
        var settings = new OracleConnectionSettings
        {
            Host = "oracle.internal",
            Port = 1521,
            Username = "migration_user",
            Password = "secret",
            ServiceName = "PRODDB"
        };

        var connectionString = OracleConnectionStringFactory.Build(settings);

        Assert.Contains("SERVICE_NAME=PRODDB", connectionString);
    }

    [Fact]
    public void BuildMasked_NeverContainsThePassword()
    {
        var settings = new OracleConnectionSettings
        {
            Host = "oracle.internal",
            Port = 1521,
            Username = "migration_user",
            Password = "TopSecret123",
            Sid = "ORCL"
        };

        var masked = OracleConnectionStringFactory.BuildMasked(settings);

        Assert.DoesNotContain("TopSecret123", masked);
        Assert.Contains("*****", masked);
        Assert.Contains("SID=ORCL", masked);
    }

    [Fact]
    public void Build_InvalidSettings_ThrowsBeforeBuildingConnectionString()
    {
        var settings = new OracleConnectionSettings
        {
            Host = "oracle.internal",
            Port = 1521,
            Username = "migration_user",
            Password = "secret"
            // neither Sid nor ServiceName set
        };

        Assert.Throws<InvalidOperationException>(() => OracleConnectionStringFactory.Build(settings));
    }
}

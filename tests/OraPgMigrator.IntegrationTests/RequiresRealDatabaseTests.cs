namespace OraPgMigrator.IntegrationTests;

/// <summary>
/// Placeholder for tests that exercise the CLI against a real Oracle instance and
/// (for `validate`) a real PostgreSQL instance. Not implemented in the MVP —
/// intentionally skipped so `dotnet test` never requires live database
/// infrastructure. When real connectivity is available, set the
/// ORAPG_IT_ORACLE_* / ORAPG_IT_POSTGRES_* environment variables and replace the
/// Skip reason with actual `scan`/`ddl`/`export`/`validate` invocations.
/// </summary>
public class RequiresRealDatabaseTests
{
    [Fact(Skip = "Requires a live Oracle instance; not available in this environment. See class remarks.")]
    public void Scan_AgainstRealOracleSchema_ProducesExpectedManifestAndMetadata()
    {
    }

    [Fact(Skip = "Requires live Oracle and PostgreSQL instances; not available in this environment. See class remarks.")]
    public void Validate_AgainstRealOracleAndPostgres_ComparesRowCounts()
    {
    }
}

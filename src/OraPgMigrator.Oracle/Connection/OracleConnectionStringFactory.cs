namespace OraPgMigrator.Oracle.Connection;

/// <summary>
/// Builds an Oracle.ManagedDataAccess.Core connection string from
/// <see cref="OracleConnectionSettings"/> so callers never need to hand-construct
/// Oracle TNS/EZConnect syntax themselves.
/// </summary>
public static class OracleConnectionStringFactory
{
    public static string Build(OracleConnectionSettings settings)
    {
        settings.Validate();

        var connectData = settings.Sid is not null ? $"SID={settings.Sid}" : $"SERVICE_NAME={settings.ServiceName}";
        var fullDescriptor =
            $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={settings.Host})(PORT={settings.Port}))(CONNECT_DATA=({connectData})))";

        var builder = new global::Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder
        {
            UserID = settings.Username,
            Password = settings.Password,
            DataSource = fullDescriptor
        };

        return builder.ConnectionString;
    }

    /// <summary>Same as <see cref="Build"/> but with the password masked, safe for logging.</summary>
    public static string BuildMasked(OracleConnectionSettings settings)
    {
        var connectData = settings.Sid is not null ? $"SID={settings.Sid}" : $"SERVICE_NAME={settings.ServiceName}";
        return $"User Id={settings.Username};Password=*****;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={settings.Host})(PORT={settings.Port}))(CONNECT_DATA=({connectData})))";
    }
}

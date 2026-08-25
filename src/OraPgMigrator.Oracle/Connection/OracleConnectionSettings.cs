using OraPgMigrator.Oracle;

namespace OraPgMigrator.Oracle.Connection;

/// <summary>
/// Oracle connection parameters. Exactly one of <see cref="Sid"/> or
/// <see cref="ServiceName"/> must be supplied. <see cref="Schema"/> is the schema
/// to inspect/export and is independent of <see cref="Username"/> — the connected
/// account may simply have privileges to read another schema.
/// </summary>
public sealed record OracleConnectionSettings
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }

    public string? Sid { get; init; }
    public string? ServiceName { get; init; }

    /// <summary>Schema to scan/export. Falls back to <see cref="Username"/> (normalized) when not set.</summary>
    public string? Schema { get; init; }

    public string ResolvedSchema => OracleIdentifiers.Normalize(Schema ?? Username);

    public void Validate()
    {
        var hasSid = !string.IsNullOrWhiteSpace(Sid);
        var hasServiceName = !string.IsNullOrWhiteSpace(ServiceName);

        if (hasSid == hasServiceName)
        {
            throw new InvalidOperationException(
                "Exactly one of Oracle SID or Service Name must be provided (not both, not neither).");
        }

        if (Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException($"Oracle port '{Port}' is not a valid TCP port.");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Oracle host must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("Oracle username must not be empty.");
        }

        if (string.IsNullOrEmpty(Password))
        {
            throw new InvalidOperationException("Oracle password must not be empty.");
        }
    }

    /// <summary>Safe-to-log representation. Never includes the password.</summary>
    public override string ToString() =>
        $"OracleConnectionSettings {{ Host = {Host}, Port = {Port}, {(Sid is not null ? $"Sid = {Sid}" : $"ServiceName = {ServiceName}")}, Username = {Username}, Schema = {ResolvedSchema}, Password = ***** }}";
}

using System.CommandLine;
using OraPgMigrator.Core.Abstractions;
using OraPgMigrator.Oracle.Connection;

namespace OraPgMigrator.Cli.Options;

/// <summary>
/// The full set of Oracle connection CLI options, shared by every command that
/// needs to talk to Oracle (scan, export, validate, migrate). Direct CLI values
/// always take precedence over the matching "*-env" option (spec §3, §59).
/// </summary>
public sealed class OracleConnectionOptionSet
{
    public Option<string?> Host { get; } = new("--oracle-host") { Description = "Oracle host name or IP address." };
    public Option<string?> HostEnv { get; } = new("--oracle-host-env") { Description = "Environment variable containing the Oracle host." };

    public Option<int?> Port { get; } = new("--oracle-port") { Description = "Oracle TCP port (default 1521)." };
    public Option<string?> PortEnv { get; } = new("--oracle-port-env") { Description = "Environment variable containing the Oracle port." };

    public Option<string?> Sid { get; } = new("--oracle-sid") { Description = "Oracle SID (mutually exclusive with --oracle-service-name)." };
    public Option<string?> SidEnv { get; } = new("--oracle-sid-env") { Description = "Environment variable containing the Oracle SID." };

    public Option<string?> ServiceName { get; } = new("--oracle-service-name") { Description = "Oracle service name (mutually exclusive with --oracle-sid)." };
    public Option<string?> ServiceNameEnv { get; } = new("--oracle-service-name-env") { Description = "Environment variable containing the Oracle service name." };

    public Option<string?> User { get; } = new("--oracle-user") { Description = "Oracle login username." };
    public Option<string?> UserEnv { get; } = new("--oracle-user-env") { Description = "Environment variable containing the Oracle username." };

    public Option<string?> Password { get; } = new("--oracle-password") { Description = "Oracle login password. Prefer --oracle-password-env; CLI arguments may be visible to other processes." };
    public Option<string?> PasswordEnv { get; } = new("--oracle-password-env") { Description = "Environment variable containing the Oracle password." };

    public Option<string?> Schema { get; } = new("--schema") { Description = "Schema to scan/export. Independent of --oracle-user. Defaults to the connected user's schema." };
    public Option<string?> SchemaEnv { get; } = new("--schema-env") { Description = "Environment variable containing the schema name." };

    public void AddTo(Command command)
    {
        command.Options.Add(Host);
        command.Options.Add(HostEnv);
        command.Options.Add(Port);
        command.Options.Add(PortEnv);
        command.Options.Add(Sid);
        command.Options.Add(SidEnv);
        command.Options.Add(ServiceName);
        command.Options.Add(ServiceNameEnv);
        command.Options.Add(User);
        command.Options.Add(UserEnv);
        command.Options.Add(Password);
        command.Options.Add(PasswordEnv);
        command.Options.Add(Schema);
        command.Options.Add(SchemaEnv);
    }

    public OracleConnectionSettings Resolve(ParseResult parseResult, IEnvironmentVariableResolver envResolver)
    {
        var host = Require("Oracle host", parseResult.GetValue(Host), parseResult.GetValue(HostEnv), envResolver);
        var user = Require("Oracle username", parseResult.GetValue(User), parseResult.GetValue(UserEnv), envResolver);
        var password = Require("Oracle password", parseResult.GetValue(Password), parseResult.GetValue(PasswordEnv), envResolver);

        var portText = Optional(parseResult.GetValue(Port)?.ToString(), parseResult.GetValue(PortEnv), envResolver);
        var port = portText is null ? 1521 : int.Parse(portText);

        var sid = Optional(parseResult.GetValue(Sid), parseResult.GetValue(SidEnv), envResolver);
        var serviceName = Optional(parseResult.GetValue(ServiceName), parseResult.GetValue(ServiceNameEnv), envResolver);
        var schema = Optional(parseResult.GetValue(Schema), parseResult.GetValue(SchemaEnv), envResolver);

        return new OracleConnectionSettings
        {
            Host = host,
            Port = port,
            Username = user,
            Password = password,
            Sid = sid,
            ServiceName = serviceName,
            Schema = schema
        };
    }

    private static string Require(string label, string? direct, string? envVarName, IEnvironmentVariableResolver envResolver)
    {
        return Optional(direct, envVarName, envResolver)
            ?? throw new InvalidOperationException($"{label} was not provided. Supply the direct option or its *-env equivalent.");
    }

    private static string? Optional(string? direct, string? envVarName, IEnvironmentVariableResolver envResolver)
    {
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        return !string.IsNullOrEmpty(envVarName) ? envResolver.GetRequired(envVarName) : null;
    }
}

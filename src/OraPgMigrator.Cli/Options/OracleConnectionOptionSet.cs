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

    public const string DefaultHostEnvVar = "ORAPG_HOST";
    public const string DefaultPortEnvVar = "ORAPG_PORT";
    public const string DefaultSidEnvVar = "ORAPG_SID";
    public const string DefaultUserEnvVar = "ORAPG_USER";
    public const string DefaultPasswordEnvVar = "ORAPG_PASSWORD";

    public OracleConnectionSettings Resolve(ParseResult parseResult, IEnvironmentVariableResolver envResolver)
    {
        var host = Require("Oracle host", parseResult.GetValue(Host), parseResult.GetValue(HostEnv), DefaultHostEnvVar, envResolver);
        var user = Require("Oracle username", parseResult.GetValue(User), parseResult.GetValue(UserEnv), DefaultUserEnvVar, envResolver);
        var password = Require("Oracle password", parseResult.GetValue(Password), parseResult.GetValue(PasswordEnv), DefaultPasswordEnvVar, envResolver);

        var portText = OptionalWithFallback(parseResult.GetValue(Port)?.ToString(), parseResult.GetValue(PortEnv), DefaultPortEnvVar, envResolver);
        var port = portText is null ? 1521 : int.Parse(portText);

        var sid = OptionalWithFallback(parseResult.GetValue(Sid), parseResult.GetValue(SidEnv), DefaultSidEnvVar, envResolver);
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

    /// <summary>
    /// Resolves a value that is always backed by an environment variable: the direct
    /// flag wins if set, otherwise the explicit "*-env" flag names the variable to read,
    /// otherwise the field's default "ORAPG_*" variable name is used. The env var named
    /// by whichever path is taken must actually be set, or resolution fails naming it.
    /// </summary>
    private static string Require(string label, string? direct, string? explicitEnvVarName, string defaultEnvVarName, IEnvironmentVariableResolver envResolver)
    {
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        var envVarName = string.IsNullOrEmpty(explicitEnvVarName) ? defaultEnvVarName : explicitEnvVarName;
        try
        {
            return envResolver.GetRequired(envVarName);
        }
        catch (InvalidOperationException) when (string.IsNullOrEmpty(explicitEnvVarName))
        {
            throw new InvalidOperationException(
                $"{label} was not provided. Set the environment variable '{defaultEnvVarName}', or pass its direct or *-env option explicitly.");
        }
    }

    /// <summary>
    /// Like <see cref="Require"/> but the default env var lookup is optional: used for
    /// fields that are mutually exclusive with an alternative (Sid vs ServiceName), so an
    /// unset default must fall through silently rather than error.
    /// </summary>
    private static string? OptionalWithFallback(string? direct, string? explicitEnvVarName, string defaultEnvVarName, IEnvironmentVariableResolver envResolver)
    {
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        if (!string.IsNullOrEmpty(explicitEnvVarName))
        {
            return envResolver.GetRequired(explicitEnvVarName);
        }

        return envResolver.GetOptional(defaultEnvVarName);
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

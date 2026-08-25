namespace OraPgMigrator.Core.Abstractions;

/// <summary>
/// Resolves configuration values from custom-named environment variables. Error
/// messages must identify the variable name but never the resolved value, since
/// this is also used to resolve secrets such as passwords.
/// </summary>
public interface IEnvironmentVariableResolver
{
    string GetRequired(string variableName);
    string? GetOptional(string variableName);
}

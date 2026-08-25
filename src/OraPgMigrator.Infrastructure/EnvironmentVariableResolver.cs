using OraPgMigrator.Core.Abstractions;

namespace OraPgMigrator.Infrastructure;

public sealed class EnvironmentVariableResolver : IEnvironmentVariableResolver
{
    public string GetRequired(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Environment variable '{variableName}' is not set.");
        }
        return value;
    }

    public string? GetOptional(string variableName) => Environment.GetEnvironmentVariable(variableName);
}

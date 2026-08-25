namespace OraPgMigrator.Core.Validation;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>A single migration risk or note surfaced to the user, never silently swallowed.</summary>
public sealed record ValidationIssue(ValidationSeverity Severity, string Category, string Message);

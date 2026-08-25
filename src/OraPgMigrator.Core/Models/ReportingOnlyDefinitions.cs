namespace OraPgMigrator.Core.Models;

/// <summary>
/// Objects that the scanner discovers and reports on but does not (yet) migrate.
/// Never claim these are "migrated" just because they were detected.
/// </summary>
public sealed record ViewDefinition(string Name, string Definition);

public sealed record TriggerDefinition(string Name, string TableName, string Body);

public sealed record SynonymDefinition(string Name, string TargetOwner, string TargetObject);

public enum RoutineKind
{
    Procedure,
    Function,
    Package,
    PackageBody
}

public sealed record RoutineDefinition(string Name, RoutineKind Kind);

public sealed record PartitioningInfo(string TableName, string PartitioningType, IReadOnlyList<string> PartitionKeyColumns);

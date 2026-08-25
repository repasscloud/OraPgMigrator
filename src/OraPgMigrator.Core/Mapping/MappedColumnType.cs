namespace OraPgMigrator.Core.Mapping;

/// <summary>Result of mapping a source column type to a target SQL type.</summary>
public sealed record MappedColumnType(string SqlType, string? Note = null);

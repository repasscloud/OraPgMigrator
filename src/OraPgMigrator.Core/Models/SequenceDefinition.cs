namespace OraPgMigrator.Core.Models;

public sealed record SequenceDefinition(
    string Name,
    long StartValue,
    long IncrementBy,
    long? MinValue,
    long? MaxValue,
    bool Cycle,
    long CacheSize);

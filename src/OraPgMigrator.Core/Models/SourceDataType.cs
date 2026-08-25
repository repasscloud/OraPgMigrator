namespace OraPgMigrator.Core.Models;

/// <summary>
/// Neutral description of a source (Oracle) column's data type, independent of any
/// target database. Carries enough information to drive type mapping without
/// requiring a live connection back to the source.
/// </summary>
public sealed record SourceDataType(
    string NativeTypeName,
    int? Length,
    int? Precision,
    int? Scale,
    bool IsCharacterUnit);

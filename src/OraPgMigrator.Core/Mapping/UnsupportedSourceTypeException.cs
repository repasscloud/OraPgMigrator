namespace OraPgMigrator.Core.Mapping;

/// <summary>
/// Thrown when a source data type cannot be safely mapped to the target database
/// and no user-supplied override exists. Callers must surface this clearly rather
/// than falling back to a guess.
/// </summary>
public sealed class UnsupportedSourceTypeException : Exception
{
    public string NativeTypeName { get; }

    public UnsupportedSourceTypeException(string nativeTypeName)
        : base($"Unsupported source data type '{nativeTypeName}'. Provide a custom type mapping or exclude the affected column/table.")
    {
        NativeTypeName = nativeTypeName;
    }
}

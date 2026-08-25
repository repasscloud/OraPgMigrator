using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Conversion;

/// <summary>
/// Dedicated pipeline for converting a raw source (Oracle) value read from a data
/// reader into a CLR value ready for target serialization (CSV today, other
/// targets later). Never rely on <c>ToString()</c> alone.
/// </summary>
public interface ISourceValueConverter
{
    /// <summary>
    /// Converts <paramref name="rawValue"/> (as produced by the source data reader)
    /// into a value suitable for the CSV writer. Returns <c>null</c> for SQL NULL.
    /// </summary>
    object? Convert(SourceDataType sourceType, object? rawValue);
}

using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Core.Mapping;

/// <summary>
/// Dedicated subsystem for translating a source (Oracle) column data type into a
/// target (PostgreSQL) SQL type. All type-conversion decisions must go through an
/// implementation of this interface rather than being scattered across the
/// codebase.
/// </summary>
public interface IDataTypeMapper
{
    /// <summary>
    /// Maps a source type to a target type. Throws <see cref="UnsupportedSourceTypeException"/>
    /// when the source type cannot be safely translated and no override applies.
    /// </summary>
    MappedColumnType Map(SourceDataType sourceType);
}

using OraPgMigrator.Core.Mapping;
using OraPgMigrator.Core.Models;

namespace OraPgMigrator.Postgres.Ddl;

/// <summary>
/// Dedicated Oracle -> PostgreSQL data type mapping subsystem (spec §12-§15/§43).
/// User-supplied <see cref="TypeMappingOverrides"/> always take precedence over the
/// built-in rules. Anything not covered here throws
/// <see cref="UnsupportedSourceTypeException"/> rather than guessing.
/// </summary>
public sealed class OracleToPostgresTypeMapper : IDataTypeMapper
{
    private readonly TypeMappingOverrides _overrides;

    public OracleToPostgresTypeMapper(TypeMappingOverrides? overrides = null)
    {
        _overrides = overrides ?? TypeMappingOverrides.Empty;
    }

    public MappedColumnType Map(SourceDataType sourceType)
    {
        var nativeType = sourceType.NativeTypeName.ToUpperInvariant().Trim();

        if (_overrides.TryGet(nativeType, out var overrideType))
        {
            return new MappedColumnType(overrideType, "User-supplied type override.");
        }

        return nativeType switch
        {
            "VARCHAR2" or "NVARCHAR2" => new MappedColumnType(
                sourceType.Length is > 0 ? $"varchar({sourceType.Length})" : "text"),

            "CHAR" or "NCHAR" => new MappedColumnType(
                sourceType.Length is > 0 ? $"char({sourceType.Length})" : "char(1)"),

            "CLOB" or "NCLOB" or "LONG" => new MappedColumnType("text"),

            "BLOB" or "RAW" or "LONG RAW" => new MappedColumnType("bytea"),

            "DATE" => new MappedColumnType("timestamp without time zone"),

            "TIMESTAMP" => new MappedColumnType("timestamp without time zone"),

            "TIMESTAMP WITH TIME ZONE" => new MappedColumnType("timestamp with time zone"),

            // Oracle's session-local-time-zone timestamp has no exact PostgreSQL analogue;
            // "timestamp with time zone" is the closest safe behavior (values are stored/compared
            // in UTC and rendered in the reading session's zone), but this must be an explicit,
            // documented policy rather than an assumption — see IMPLEMENTATION_PLAN.md.
            "TIMESTAMP WITH LOCAL TIME ZONE" => new MappedColumnType(
                "timestamp with time zone", "Oracle TIMESTAMP WITH LOCAL TIME ZONE mapped to timestamptz; verify session time zone handling."),

            "INTERVAL YEAR TO MONTH" => new MappedColumnType("interval"),
            "INTERVAL DAY TO SECOND" => new MappedColumnType("interval"),

            "FLOAT" => new MappedColumnType("double precision"),
            "BINARY_FLOAT" => new MappedColumnType("real"),
            "BINARY_DOUBLE" => new MappedColumnType("double precision"),

            "NUMBER" => MapNumber(sourceType),

            "XMLTYPE" => throw new UnsupportedSourceTypeException(nativeType),
            "ROWID" or "UROWID" => throw new UnsupportedSourceTypeException(nativeType),
            "SDO_GEOMETRY" => throw new UnsupportedSourceTypeException(nativeType),

            _ => throw new UnsupportedSourceTypeException(nativeType)
        };
    }

    private static MappedColumnType MapNumber(SourceDataType sourceType)
    {
        if (sourceType.Precision is null)
        {
            return new MappedColumnType("numeric");
        }

        if (sourceType.Scale is null or 0)
        {
            return sourceType.Precision switch
            {
                <= 4 => new MappedColumnType("smallint"),
                <= 9 => new MappedColumnType("integer"),
                <= 18 => new MappedColumnType("bigint"),
                _ => new MappedColumnType($"numeric({sourceType.Precision})")
            };
        }

        return new MappedColumnType($"numeric({sourceType.Precision},{sourceType.Scale})");
    }
}

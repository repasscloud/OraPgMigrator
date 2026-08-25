using OraPgMigrator.Core.Conversion;
using OraPgMigrator.Core.Models;
using OracleClient = global::Oracle.ManagedDataAccess.Client;
using OracleTypes = global::Oracle.ManagedDataAccess.Types;

namespace OraPgMigrator.Oracle.Conversion;

/// <summary>
/// Unwraps Oracle provider-specific value types into plain CLR values ready for
/// downstream serialization. Never relies on <c>ToString()</c> for numeric/date
/// values, and always uses invariant culture semantics (the CLR values themselves
/// carry no culture; formatting happens at the CSV writer).
/// </summary>
public sealed class OracleValueConverter : ISourceValueConverter
{
    public object? Convert(SourceDataType sourceType, object? rawValue)
    {
        switch (rawValue)
        {
            case null or DBNull:
                return null;

            case OracleTypes.OracleDecimal oracleDecimal:
                return oracleDecimal.IsNull ? null : oracleDecimal.Value;

            case OracleTypes.OracleDate oracleDate:
                return oracleDate.IsNull ? null : oracleDate.Value;

            case OracleTypes.OracleTimeStamp oracleTimeStamp:
                return oracleTimeStamp.IsNull ? null : oracleTimeStamp.Value;

            case OracleTypes.OracleTimeStampTZ oracleTimeStampTz:
                return oracleTimeStampTz.IsNull ? null : new DateTimeOffset(oracleTimeStampTz.Value, oracleTimeStampTz.GetTimeZoneOffset());

            case OracleTypes.OracleTimeStampLTZ oracleTimeStampLtz:
                return oracleTimeStampLtz.IsNull ? null : oracleTimeStampLtz.ToOracleTimeStamp().Value;

            case OracleTypes.OracleString oracleString:
                return oracleString.IsNull ? null : oracleString.Value;

            case OracleTypes.OracleBinary oracleBinary:
                return oracleBinary.IsNull ? null : oracleBinary.Value;

            case OracleTypes.OracleIntervalYM intervalYm:
                return intervalYm.IsNull ? null : FormatIntervalYearToMonth(intervalYm);

            case OracleTypes.OracleIntervalDS intervalDs:
                return intervalDs.IsNull ? null : intervalDs.Value;

            case byte[] bytes:
                return bytes;

            case string str:
                return str;

            case decimal or DateTime or DateTimeOffset or double or float or bool or TimeSpan:
                return rawValue;

            default:
                return rawValue;
        }
    }

    private static string FormatIntervalYearToMonth(OracleTypes.OracleIntervalYM interval) =>
        $"{interval.Years} years {interval.Months} mons";
}

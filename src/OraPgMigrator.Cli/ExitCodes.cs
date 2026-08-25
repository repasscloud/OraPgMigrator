namespace OraPgMigrator.Cli;

/// <summary>Deterministic CLI exit codes (spec §51).</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int InvalidArguments = 2;
    public const int OracleConnectionFailure = 3;
    public const int SchemaAccessFailure = 4;
    public const int OutputStorageFailure = 5;
    public const int UnsupportedSchemaOrData = 6;
    public const int ValidationFailure = 7;
}

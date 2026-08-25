namespace OraPgMigrator.Core.Migration;

public enum TableExportStatus
{
    Pending,
    Complete,
    Failed
}

public sealed record TableExportState(TableExportStatus Status, long? RowsWritten, DateTimeOffset? UpdatedAtUtc);

/// <summary>
/// Table-granularity resume state, persisted to migration-state.json. A `.partial`
/// data file must never be treated as complete, regardless of what this file says
/// (spec §32) — completion is only ever recorded after a successful atomic rename.
/// </summary>
public sealed class MigrationState
{
    public Dictionary<string, TableExportState> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsComplete(string tableKey) =>
        Tables.TryGetValue(tableKey, out var state) && state.Status == TableExportStatus.Complete;
}

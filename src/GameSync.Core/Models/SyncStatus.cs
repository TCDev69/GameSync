namespace GameSync.Core.Models;

/// <summary>
/// High-level synchronization state for a game or repository operation.
/// </summary>
public enum SyncStatus
{
    Unknown = 0,
    UpToDate = 1,
    BehindRemote = 2,
    AheadOfRemote = 3,
    Diverged = 4,
    Conflicted = 5,
    LocalChanges = 6,
    Syncing = 7,
    Failed = 8
}

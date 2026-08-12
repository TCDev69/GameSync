namespace GameSync.Core.Models;

/// <summary>
/// UI-facing per-game library status. Derived from sync state + local configuration.
/// </summary>
public enum GameLibraryStatus
{
    Unknown = 0,
    Synced = 1,
    LocalChanges = 2,
    RemoteChanges = 3,
    Conflict = 4,
    Running = 5,
    Syncing = 6,
    Error = 7,
    NotConfigured = 8
}

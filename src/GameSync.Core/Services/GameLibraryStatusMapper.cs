using GameSync.Core.Models;

namespace GameSync.Core.Services;

public static class GameLibraryStatusMapper
{
    public static GameLibraryStatus FromSyncStatus(SyncStatus status, bool hasLocalExecutable)
    {
        if (!hasLocalExecutable)
        {
            return GameLibraryStatus.NotConfigured;
        }

        return status switch
        {
            SyncStatus.UpToDate => GameLibraryStatus.Synced,
            SyncStatus.LocalChanges or SyncStatus.AheadOfRemote => GameLibraryStatus.LocalChanges,
            SyncStatus.BehindRemote => GameLibraryStatus.RemoteChanges,
            SyncStatus.Conflicted or SyncStatus.Diverged => GameLibraryStatus.Conflict,
            SyncStatus.Syncing => GameLibraryStatus.Syncing,
            SyncStatus.Failed => GameLibraryStatus.Error,
            _ => GameLibraryStatus.Unknown
        };
    }

    public static string ToDisplayText(GameLibraryStatus status) => status switch
    {
        GameLibraryStatus.Synced => "Synced",
        GameLibraryStatus.LocalChanges => "Local Changes",
        GameLibraryStatus.RemoteChanges => "Remote Changes",
        GameLibraryStatus.Conflict => "Conflict",
        GameLibraryStatus.Running => "Running",
        GameLibraryStatus.Syncing => "Syncing",
        GameLibraryStatus.Error => "Error",
        GameLibraryStatus.NotConfigured => "Not Configured",
        _ => "Unknown"
    };
}

namespace GameSync.Core.Models;

public static class LaunchPhaseProgress
{
    public static (double Percent, bool IsIndeterminate) Map(LaunchPhase phase) =>
        phase switch
        {
            LaunchPhase.Preparing => (5, false),
            LaunchPhase.CheckingRepository => (12, false),
            LaunchPhase.DownloadingSaves => (25, false),
            LaunchPhase.RestoringSaves => (40, false),
            LaunchPhase.LaunchingGame => (52, false),
            LaunchPhase.GameRunning => (62, true),
            LaunchPhase.AwaitingSessionEnd => (68, true),
            LaunchPhase.GameClosed => (82, false),
            LaunchPhase.SavingChanges => (88, false),
            LaunchPhase.UploadingChanges => (94, false),
            LaunchPhase.Completed => (100, false),
            LaunchPhase.Error => (0, false),
            LaunchPhase.Cancelled => (0, false),
            _ => (0, true)
        };

    public static string Describe(LaunchPhase phase, string? fallbackMessage) =>
        phase switch
        {
            LaunchPhase.Preparing => "Preparing launch",
            LaunchPhase.CheckingRepository => "Checking repository",
            LaunchPhase.DownloadingSaves => "Downloading saves from cloud",
            LaunchPhase.RestoringSaves => "Restoring saves locally",
            LaunchPhase.LaunchingGame => "Starting the game",
            LaunchPhase.GameRunning => "Game is running",
            LaunchPhase.AwaitingSessionEnd => "Waiting for you to finish playing",
            LaunchPhase.GameClosed => "Game closed",
            LaunchPhase.SavingChanges => "Saving changes",
            LaunchPhase.UploadingChanges => "Uploading saves to cloud",
            LaunchPhase.Completed => "Launch completed",
            LaunchPhase.Error => fallbackMessage ?? "Launch failed",
            LaunchPhase.Cancelled => "Launch cancelled",
            _ => fallbackMessage ?? "Working"
        };
}

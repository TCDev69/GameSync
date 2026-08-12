namespace GameSync.Core.Models;

/// <summary>
/// Observable phases for the lightweight launcher status UI.
/// </summary>
public enum LaunchPhase
{
    Preparing = 0,
    CheckingRepository = 1,
    DownloadingSaves = 2,
    RestoringSaves = 3,
    LaunchingGame = 4,
    GameRunning = 5,
    GameClosed = 6,
    SavingChanges = 7,
    UploadingChanges = 8,
    Completed = 9,
    Error = 10,
    Cancelled = 11,
    AwaitingSessionEnd = 12
}

namespace GameSync.Core.Models;

public sealed class SteamInstalledGame
{
    public required string AppId { get; init; }

    public required string Title { get; init; }

    public required string InstallDir { get; init; }

    public required string LibraryRoot { get; init; }

    public IReadOnlyList<string> CandidateExecutables { get; init; } = [];

    public string? SuggestedMonitorExecutable { get; init; }
}

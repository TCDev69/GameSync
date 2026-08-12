namespace GameSync.Core.Commands;

/// <summary>
/// Application entry mode derived from CLI arguments.
/// </summary>
public enum AppCommandKind
{
    Dashboard = 0,
    LaunchGame = 1,
    SyncAll = 2,
    SyncGame = 3,
    Status = 4,
    Settings = 5,
    Help = 6,
    CheckUpdate = 7,
    InstallUpdate = 8
}

/// <summary>
/// Parsed command-line request. UI-agnostic.
/// </summary>
public sealed class AppCommand
{
    public required AppCommandKind Kind { get; init; }

    public string? GameId { get; init; }

    public IReadOnlyList<string> RawArguments { get; init; } = Array.Empty<string>();
}

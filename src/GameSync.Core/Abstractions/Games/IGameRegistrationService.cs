using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Games;

public sealed class GameRegistrationRequest
{
    public required string Title { get; init; }
    public string? CoverUrl { get; init; }
    public string? MetadataProviderId { get; init; }
    public string? MetadataExternalId { get; init; }
    public IReadOnlyList<SaveLocation> SaveLocations { get; init; } = [];
    public string? Executable { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string MonitorExecutable { get; init; } = string.Empty;

    /// <summary>
    /// If set, overrides the auto-generated game ID.
    /// </summary>
    public string? GameIdOverride { get; init; }
}

public enum DuplicateGameAction
{
    Skip,
    ImportAsNew,
    UpdateLaunchOnly
}

public sealed class GameRegistrationResult
{
    public required string GameId { get; init; }
    public bool WasSkipped { get; init; }
    public bool WasUpdatedOnly { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => ErrorMessage is null && !WasSkipped;
}

public interface IGameRegistrationService
{
    Task<GameRegistrationResult> RegisterGameAsync(
        GameRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<GameRegistrationResult> RegisterWithDuplicateActionAsync(
        GameRegistrationRequest request,
        DuplicateGameAction action,
        CancellationToken cancellationToken = default);

    IReadOnlyList<Game> FindDuplicateCandidates(
        IReadOnlyList<Game> existingGames,
        string title,
        string? metadataExternalId);
}

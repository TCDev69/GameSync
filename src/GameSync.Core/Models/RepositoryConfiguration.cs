namespace GameSync.Core.Models;

/// <summary>
/// Describes the GitHub repository used as the GameSync cloud backend.
/// Credentials are never stored here; see ICredentialStore.
/// </summary>
public sealed record RepositoryConfiguration
{
    public required string Owner { get; init; }

    public required string Name { get; init; }

    public string? CloneUrl { get; init; }

    public string DefaultBranch { get; init; } = "main";

    /// <summary>
    /// Absolute path to the local clone under %LOCALAPPDATA%\GameSync\repositories\.
    /// </summary>
    public string? LocalPath { get; init; }

    public bool IsPrivate { get; init; } = true;
}

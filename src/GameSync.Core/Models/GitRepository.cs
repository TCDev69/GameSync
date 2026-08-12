namespace GameSync.Core.Models;

/// <summary>
/// Runtime view of a local Git clone used by GameSync.
/// </summary>
public sealed class GitRepository
{
    public required string LocalPath { get; init; }

    public string? RemoteUrl { get; init; }

    public string? CurrentBranch { get; init; }

    public string? HeadCommitSha { get; init; }

    public bool HasUncommittedChanges { get; init; }

    public SyncStatus SyncStatus { get; init; } = SyncStatus.Unknown;
}

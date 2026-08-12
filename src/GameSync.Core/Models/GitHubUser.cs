namespace GameSync.Core.Models;

/// <summary>
/// Authenticated GitHub user profile (non-secret).
/// </summary>
public sealed class GitHubUser
{
    public required string Login { get; init; }

    public long Id { get; init; }

    public string? Name { get; init; }

    public string? AvatarUrl { get; init; }
}

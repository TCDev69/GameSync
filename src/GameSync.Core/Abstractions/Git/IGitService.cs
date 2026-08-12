using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Git;

/// <summary>
/// Embedded Git operations. Implementations must not require a user-installed git.exe.
/// </summary>
public interface IGitService
{
    Task CloneAsync(string remoteUrl, string localPath, CancellationToken cancellationToken = default);

    Task PullAsync(string localPath, CancellationToken cancellationToken = default);

    Task FetchAsync(string localPath, CancellationToken cancellationToken = default);

    Task<GitRepository> GetStatusAsync(string localPath, CancellationToken cancellationToken = default);

    Task AddAsync(string localPath, IEnumerable<string> paths, CancellationToken cancellationToken = default);

    Task CommitAsync(string localPath, string message, CancellationToken cancellationToken = default);

    Task PushAsync(string localPath, CancellationToken cancellationToken = default);

    Task ResetAsync(string localPath, string commitSha, bool hard, CancellationToken cancellationToken = default);

    Task CheckoutAsync(string localPath, string commitShaOrBranch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores specific paths in the working tree from a historical commit without moving HEAD.
    /// </summary>
    Task CheckoutPathsAsync(string localPath, string commitSha, IEnumerable<string> paths, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaveHistoryEntry>> GetHistoryAsync(string localPath, string? pathFilter = null, int maxCount = 50, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Conflict>> GetConflictsAsync(string localPath, CancellationToken cancellationToken = default);
}

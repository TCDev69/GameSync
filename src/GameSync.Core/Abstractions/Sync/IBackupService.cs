using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

public interface IBackupService
{
    Task<string> CreateBackupAsync(string gameId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default);

    Task PruneAsync(string gameId, int? maxBackupsOverride = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListBackupsAsync(string gameId, CancellationToken cancellationToken = default);
}

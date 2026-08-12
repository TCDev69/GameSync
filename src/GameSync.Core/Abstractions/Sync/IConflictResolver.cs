using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

public interface IConflictResolver
{
    /// <summary>
    /// Applies a conflict resolution without prompting. UI supplies the choice.
    /// </summary>
    Task ApplyAsync(string repositoryLocalPath, Conflict conflict, ConflictResolution resolution, CancellationToken cancellationToken = default);

    ConflictResolution ToResolution(ConflictResolutionChoice choice);
}

using GameSync.Core.Models;

namespace GameSync.Core.Errors;

public sealed class GitConflictDetectedException : GameSyncException
{
    public IReadOnlyList<Conflict> Conflicts { get; }

    public GitConflictDetectedException(string message, IReadOnlyList<Conflict> conflicts)
        : base("GitConflictDetected", message)
    {
        Conflicts = conflicts;
    }
}

namespace GameSync.Core.Models;

/// <summary>
/// Explicit conflict resolution actions exposed to higher layers / UI.
/// Maps to <see cref="ConflictResolutionChoice"/>.
/// </summary>
public enum ConflictResolution
{
    Abort = 0,
    Local = 1,
    Remote = 2
}

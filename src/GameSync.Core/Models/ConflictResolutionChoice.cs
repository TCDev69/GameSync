namespace GameSync.Core.Models;

/// <summary>
/// User-selected resolution for a non-mergeable (typically binary) save conflict.
/// </summary>
public enum ConflictResolutionChoice
{
    Cancel = 0,
    UseLocal = 1,
    UseRemote = 2,
    ViewHistory = 3
}

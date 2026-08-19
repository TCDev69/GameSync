namespace GameSync.Core.Models;

/// <summary>
/// Classification of a Git conflict. Binary save conflicts are never auto-merged.
/// </summary>
public enum ConflictType
{
    Content = 0,
    ModifyDelete = 1,
    AddAdd = 2,
    Rename = 3,
    Unknown = 4,
    SaveDivergence = 5
}

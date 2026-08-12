namespace GameSync.Core.Models;

/// <summary>
/// Distinguishes whether a save location maps a single file or a directory tree.
/// </summary>
public enum SaveLocationType
{
    Directory = 0,
    File = 1
}

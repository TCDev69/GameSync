namespace GameSync.Core.Models;

/// <summary>
/// Categories of long-running UI operations. Higher values take precedence in the global activity banner.
/// </summary>
public enum AppActivityKind
{
    Library = 10,
    Sync = 80,
    Launch = 100
}

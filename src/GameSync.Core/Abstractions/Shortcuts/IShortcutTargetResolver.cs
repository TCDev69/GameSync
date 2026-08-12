namespace GameSync.Core.Abstractions.Shortcuts;

public interface IShortcutTargetResolver
{
    string? TryResolveTargetPath(string path);
}

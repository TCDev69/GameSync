namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Waits for an OS process to start and exit (used after protocol-based game launches).
/// </summary>
public interface IGameProcessWatcher
{
    Task<int> WaitForProcessExitByNameAsync(
        string processName,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken = default);
}

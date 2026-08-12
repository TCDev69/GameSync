namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Compatibility monitor surface implemented by <c>WindowsProcessLauncher</c>.
/// Prefer <see cref="IProcessLauncher"/> for new code.
/// </summary>
public interface IProcessMonitor
{
    Task<int> StartAndWaitAsync(
        string executablePath,
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default);
}

namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Starts a protocol URI via the shell (e.g. steam://).
/// </summary>
public interface IProtocolLauncher
{
    Task LaunchAsync(string uri, CancellationToken cancellationToken = default);
}

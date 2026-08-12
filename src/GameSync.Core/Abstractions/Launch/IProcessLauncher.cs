namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Abstraction over OS process creation so unit tests never start real games.
/// </summary>
public interface IProcessLauncher
{
    Task<ILaunchedProcess> StartAsync(ProcessStartRequest request, CancellationToken cancellationToken = default);
}

public interface ILaunchedProcess : IAsyncDisposable
{
    int Id { get; }

    bool HasExited { get; }

    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);
}

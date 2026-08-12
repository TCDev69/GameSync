using GameSync.Core.Abstractions.Launch;

namespace GameSync.App.Services;

public sealed class UiGameSessionAwaiter : IGameSessionAwaiter
{
    private TaskCompletionSource? _completion;

    public Task WaitForSessionEndAsync(string gameTitle, CancellationToken cancellationToken = default)
    {
        _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        }

        return _completion.Task;
    }

    public void CompleteSession() => _completion?.TrySetResult();
}

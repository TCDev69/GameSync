using GameSync.App.Dialogs;
using GameSync.App.Navigation;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.Services;

public sealed class ConflictResolutionUiService
{
    private readonly IConflictResolver _resolver;
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISyncWorkflow _syncWorkflow;
    private readonly INavigationService _navigation;

    public ConflictResolutionUiService(
        IConflictResolver resolver,
        IMachineConfigurationStore machineStore,
        ISyncWorkflow syncWorkflow,
        INavigationService navigation)
    {
        _resolver = resolver;
        _machineStore = machineStore;
        _syncWorkflow = syncWorkflow;
        _navigation = navigation;
    }

    public async Task<bool> TryResolveAsync(
        XamlRoot xamlRoot,
        SyncResult result,
        string? gameTitle,
        Func<Task>? retryAsync = null,
        bool navigateToHistoryFromLauncher = false)
    {
        if (result.Conflicts.Count == 0)
        {
            return false;
        }

        var conflict = result.Conflicts[0];
        var dialog = new ConflictDialog { XamlRoot = xamlRoot };
        dialog.Load(conflict, gameTitle);
        await dialog.ShowAsync();

        if (dialog.Choice is ConflictResolutionChoice.ViewHistory)
        {
            if (navigateToHistoryFromLauncher)
            {
                var info = new ContentDialog
                {
                    XamlRoot = xamlRoot,
                    Title = "History is in main window",
                    Content = "Open the main GameSync window and go to History to inspect previous saves.",
                    CloseButtonText = "OK"
                };
                await info.ShowAsync();
            }
            else
            {
                _navigation.NavigateTo(typeof(Views.HistoryPage));
            }

            return false;
        }

        if (dialog.Choice is ConflictResolutionChoice.Cancel)
        {
            return false;
        }

        try
        {
            var resolution = _resolver.ToResolution(dialog.Choice);
            SyncResult applyResult;
            if (conflict.Type == ConflictType.SaveDivergence && !string.IsNullOrWhiteSpace(conflict.GameId))
            {
                applyResult = await _syncWorkflow.ResolveSaveDivergenceAsync(conflict.GameId, resolution)
                    .ConfigureAwait(true);
            }
            else
            {
                var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
                var repoPath = machine.Repository?.LocalPath;
                if (string.IsNullOrWhiteSpace(repoPath))
                {
                    return false;
                }

                await _resolver.ApplyAsync(repoPath, conflict, resolution).ConfigureAwait(true);
                applyResult = SyncResult.Success(SyncStatus.UpToDate);
            }

            if (!applyResult.Succeeded)
            {
                throw applyResult.Error ?? new InvalidOperationException(applyResult.Message ?? "Could not resolve conflict.");
            }

            if (retryAsync is not null)
            {
                await retryAsync().ConfigureAwait(true);
            }

            return true;
        }
        catch (Exception ex)
        {
            var error = new ContentDialog
            {
                Title = "Could not apply resolution",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = xamlRoot
            };
            await error.ShowAsync();
            return false;
        }
    }
}

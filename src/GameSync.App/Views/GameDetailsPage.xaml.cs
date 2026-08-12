using GameSync.App.Dialogs;
using GameSync.App.Navigation;
using GameSync.App.Services;
using GameSync.App.ViewModels;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace GameSync.App.Views;

public sealed partial class GameDetailsPage : Page
{
    public GameDetailsViewModel ViewModel { get; }

    public GameDetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<GameDetailsViewModel>();
        InitializeComponent();
        ViewModel.ConflictDetected += OnConflictDetected;
        Unloaded += (_, _) => ViewModel.ConflictDetected -= OnConflictDetected;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string gameId)
        {
            await ViewModel.LoadAsync(gameId);
        }
    }

    private async void OnConflictDetected(object? sender, SyncResult result)
    {
        var conflict = result.Conflicts[0];
        var dialog = new ConflictDialog();
        dialog.XamlRoot = XamlRoot;
        dialog.Load(conflict, ViewModel.Title);
        await dialog.ShowAsync();

        if (dialog.Choice is ConflictResolutionChoice.ViewHistory)
        {
            App.Services.GetRequiredService<INavigationService>().NavigateTo(typeof(HistoryPage));
            return;
        }

        if (dialog.Choice is ConflictResolutionChoice.Cancel)
        {
            return;
        }

        try
        {
            var machine = await App.Services.GetRequiredService<IMachineConfigurationStore>().LoadAsync();
            var repoPath = machine.Repository?.LocalPath;
            if (string.IsNullOrWhiteSpace(repoPath))
            {
                return;
            }

            var resolver = App.Services.GetRequiredService<IConflictResolver>();
            var resolution = resolver.ToResolution(dialog.Choice);
            await resolver.ApplyAsync(repoPath, conflict, resolution);
            await ViewModel.SyncCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            var error = new ContentDialog
            {
                Title = "Could not apply resolution",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await error.ShowAsync();
        }
    }

    private async void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        PickerInterop.Attach(picker);
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            ViewModel.ApplyLaunchPath(file.Path);
        }
    }

    private void ExecutableDropBorder_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ExecutableDropBorder_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count > 0 && items[0] is StorageFile file)
        {
            ViewModel.ApplyLaunchPath(file.Path);
        }
    }

    private void RemoveSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SaveLocationEditItem item })
        {
            ViewModel.RemoveSaveLocationCommand.Execute(item);
        }
    }
}

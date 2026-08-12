using GameSync.App.ViewModels;
using GameSync.Core.Abstractions.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GameSync.App.Views;

public sealed partial class SteamImportPage : Page
{
    public SteamImportViewModel ViewModel { get; }

    public SteamImportPage()
    {
        ViewModel = App.Services.GetRequiredService<SteamImportViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ScanCommand.Execute(null);
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SteamImportViewModel.PendingDuplicate))
        {
            return;
        }

        var pending = ViewModel.PendingDuplicate;
        if (pending is null)
        {
            return;
        }

        DuplicateDialog.Content = $"\"{pending.Title}\" is already in your library. What would you like to do?";
        var result = await DuplicateDialog.ShowAsync();

        var action = result switch
        {
            ContentDialogResult.Primary => DuplicateGameAction.ImportAsNew,
            ContentDialogResult.Secondary => DuplicateGameAction.UpdateLaunchOnly,
            _ => DuplicateGameAction.Skip
        };

        ViewModel.ResolveDuplicate(action);
    }
}

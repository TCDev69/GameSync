using GameSync.App.Navigation;
using GameSync.Core.Abstractions.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GameSync.App.Views;

public sealed partial class SyncStatusPage : Page
{
    public SyncStatusPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        BusyRing.IsActive = true;
        try
        {
            var sync = App.Services.GetRequiredService<ISyncWorkflow>();
            var status = await sync.GetStatusAsync();
            StatusText.Text = $"Repository status: {status}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            BusyRing.IsActive = false;
        }
    }

    private void OpenHistory_Click(object sender, RoutedEventArgs e) =>
        App.Services.GetRequiredService<INavigationService>().NavigateTo(typeof(HistoryPage));
}

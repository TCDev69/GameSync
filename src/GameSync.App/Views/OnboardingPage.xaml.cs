using GameSync.App.ViewModels;
using GameSync.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.Views;

public sealed partial class OnboardingPage : Page
{
    public OnboardingViewModel ViewModel { get; }

    public OnboardingPage()
    {
        ViewModel = App.Services.GetRequiredService<OnboardingViewModel>();
        InitializeComponent();
        ViewModel.Completed += OnCompleted;
        Unloaded += (_, _) => ViewModel.Completed -= OnCompleted;
    }

    private void OnCompleted(object? sender, EventArgs e)
    {
        if (App.MainWindow is MainWindow main)
        {
            main.ShowMainNavigation();
        }
    }

    private async void OpenVerification_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.VerificationUri)
            || !Uri.TryCreate(ViewModel.VerificationUri, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            var launcher = App.Services.GetRequiredService<IUriLauncher>();
            await launcher.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            ViewModel.InfoBarMessage = ex.Message;
            ViewModel.InfoBarSeverity = InfoBarSeverity.Error;
            ViewModel.IsInfoBarOpen = true;
        }
    }
}

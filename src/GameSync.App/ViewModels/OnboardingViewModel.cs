using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Services;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public enum OnboardingStep
{
    Welcome = 0,
    ConnectGitHub = 1,
    SelectRepository = 2,
    Initialize = 3,
    Done = 4
}

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly IGitHubAuthenticationService _auth;
    private readonly IGitHubService _gitHub;
    private readonly IGitHubRepositoryConnectionService _connection;
    private readonly IUiSettingsStore _uiSettingsStore;
    private readonly ILogger<OnboardingViewModel> _logger;
    private GitHubDeviceAuthorization? _deviceAuth;
    private readonly List<RepositoryConfiguration> _allRepositories = [];

    public OnboardingViewModel(
        IGitHubAuthenticationService auth,
        IGitHubService gitHub,
        IGitHubRepositoryConnectionService connection,
        IUiSettingsStore uiSettingsStore,
        ILogger<OnboardingViewModel> logger)
    {
        _auth = auth;
        _gitHub = gitHub;
        _connection = connection;
        _uiSettingsStore = uiSettingsStore;
        _logger = logger;
    }

    /// <summary>
    /// Filtered repository list shown in the UI (see <see cref="RepositorySearchQuery"/>).
    /// </summary>
    public ObservableCollection<RepositoryConfiguration> Repositories { get; } = [];

    public event EventHandler? Completed;

    [ObservableProperty]
    public partial OnboardingStep Step { get; set; } = OnboardingStep.Welcome;

    [ObservableProperty]
    public partial string UserCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VerificationUri { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RepositoryConfiguration? SelectedRepository { get; set; }

    [ObservableProperty]
    public partial string RepositorySearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    public bool IsWelcome => Step == OnboardingStep.Welcome;
    public bool IsConnect => Step == OnboardingStep.ConnectGitHub;
    public bool IsSelectRepo => Step == OnboardingStep.SelectRepository;
    public bool IsInitialize => Step == OnboardingStep.Initialize;

    partial void OnStepChanged(OnboardingStep value)
    {
        OnPropertyChanged(nameof(IsWelcome));
        OnPropertyChanged(nameof(IsConnect));
        OnPropertyChanged(nameof(IsSelectRepo));
        OnPropertyChanged(nameof(IsInitialize));

        if (value == OnboardingStep.SelectRepository)
        {
            StatusMessage = string.Empty;
        }
    }

    partial void OnRepositorySearchQueryChanged(string value) => ApplyRepositoryFilter();

    [RelayCommand]
    private void Start() => Step = OnboardingStep.ConnectGitHub;

    private bool CanCopyUserCode() => !string.IsNullOrWhiteSpace(UserCode);

    [RelayCommand(CanExecute = nameof(CanCopyUserCode))]
    private void CopyUserCode()
    {
        try
        {
            ClipboardText.SetText(UserCode);
            IsInfoBarOpen = false;
            StatusMessage = "Login code copied.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clipboard copy failed");
            ShowError("Could not copy the login code. Select the code and copy it manually (Ctrl+C).");
        }
    }

    partial void OnUserCodeChanged(string value) => CopyUserCodeCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task BeginDeviceFlowAsync()
    {
        IsBusy = true;
        StatusMessage = "Starting GitHub device login…";
        try
        {
            _deviceAuth = await _auth.StartAuthenticationAsync().ConfigureAwait(true);
            UserCode = _deviceAuth.UserCode;
            VerificationUri = _deviceAuth.VerificationUri;
            await _auth.OpenAuthenticationUrlAsync(_deviceAuth).ConfigureAwait(true);
            StatusMessage = "Enter the code on GitHub, then continue.";
            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device flow start failed");
            ShowError("GitHub authentication failed. Check your internet connection and try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CompleteDeviceFlowAsync()
    {
        if (_deviceAuth is null)
        {
            ShowError("Start GitHub login first.");
            return;
        }

        IsBusy = true;
        StatusMessage = "Waiting for GitHub authorization…";
        try
        {
            await _auth.CompleteAuthenticationAsync(_deviceAuth).ConfigureAwait(true);
            StatusMessage = "Loading repositories…";
            await LoadRepositoriesAsync().ConfigureAwait(true);
            Step = OnboardingStep.SelectRepository;
            StatusMessage = string.Empty;
            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device flow completion failed");
            ShowError("Authorization did not complete. Confirm the code on GitHub and try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshRepositoriesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading repositories…";
        try
        {
            await LoadRepositoriesAsync().ConfigureAwait(true);
            StatusMessage = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectRepositoryAsync()
    {
        if (SelectedRepository is null)
        {
            ShowError("Select a repository.");
            return;
        }

        IsBusy = true;
        Step = OnboardingStep.Initialize;
        StatusMessage = "Verifying access and preparing GameSync…";
        try
        {
            var result = await _connection.ConnectRepositoryAsync(SelectedRepository).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                Step = OnboardingStep.SelectRepository;
                ShowError(result.Message ?? result.Error?.Message ?? "Could not connect repository.");
                return;
            }

            StatusMessage = "Repository ready.";
            var ui = await _uiSettingsStore.LoadAsync().ConfigureAwait(true);
            ui.OnboardingCompleted = true;
            await _uiSettingsStore.SaveAsync(ui).ConfigureAwait(true);
            Step = OnboardingStep.Done;
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repository connect failed");
            Step = OnboardingStep.SelectRepository;
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadRepositoriesAsync()
    {
        _allRepositories.Clear();
        var list = await _gitHub.GetRepositoriesAsync().ConfigureAwait(true);
        _allRepositories.AddRange(list.OrderBy(r => r.Owner).ThenBy(r => r.Name));
        ApplyRepositoryFilter();

        if (_allRepositories.Count == 0)
        {
            ShowError("No repositories found. Create a private repository on GitHub, then refresh.");
        }
    }

    private void ApplyRepositoryFilter()
    {
        var query = RepositorySearchQuery.Trim();
        Repositories.Clear();

        IEnumerable<RepositoryConfiguration> source = _allRepositories;
        if (!string.IsNullOrEmpty(query))
        {
            source = _allRepositories.Where(r =>
                r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.Owner.Contains(query, StringComparison.OrdinalIgnoreCase)
                || $"{r.Owner}/{r.Name}".Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var repo in source)
        {
            Repositories.Add(repo);
        }

        if (SelectedRepository is not null
            && !Repositories.Any(r =>
                string.Equals(r.Owner, SelectedRepository.Owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Name, SelectedRepository.Name, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedRepository = null;
        }
    }

    private void ShowError(string message)
    {
        InfoBarMessage = message;
        InfoBarSeverity = InfoBarSeverity.Error;
        IsInfoBarOpen = true;
    }
}

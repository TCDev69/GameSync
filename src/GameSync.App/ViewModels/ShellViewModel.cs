using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace GameSync.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineConfigurationStore;
    private readonly ILocalAppDataPaths _localAppDataPaths;
    private readonly ILogger<ShellViewModel> _logger;

    public ShellViewModel(
        IMachineConfigurationStore machineConfigurationStore,
        ILocalAppDataPaths localAppDataPaths,
        ILogger<ShellViewModel> logger)
    {
        _machineConfigurationStore = machineConfigurationStore;
        _localAppDataPaths = localAppDataPaths;
        _logger = logger;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "GameSync";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty]
    public partial string MachineId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalDataPath { get; set; } = string.Empty;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _localAppDataPaths.EnsureCreated();
            LocalDataPath = _localAppDataPaths.Root;

            var machine = await _machineConfigurationStore.LoadAsync().ConfigureAwait(true);
            MachineId = machine.MachineId;
            StatusMessage = $"Machine '{MachineId}' ready";
            _logger.LogInformation("Shell initialized for machine {MachineId}", MachineId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize shell");
            StatusMessage = ex.Message;
        }
    }
}

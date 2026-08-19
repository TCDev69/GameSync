using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Models;
using GameSync.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameSync.Infrastructure.Configuration;

public sealed class JsonUiSettingsStore : IUiSettingsStore
{
    private readonly ILocalAppDataPaths _paths;
    private readonly ILogger<JsonUiSettingsStore> _logger;
    private readonly string _filePath;

    public JsonUiSettingsStore(ILocalAppDataPaths paths, ILogger<JsonUiSettingsStore> logger)
    {
        _paths = paths;
        _logger = logger;
        _filePath = Path.Combine(paths.Root, "ui.json");
    }

    public async Task<UiSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        if (!File.Exists(_filePath))
        {
            return new UiSettings();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync(
                   stream,
                   GameSyncJsonContext.Default.UiSettings,
                   cancellationToken).ConfigureAwait(false)
               ?? new UiSettings();
    }

    public async Task SaveAsync(UiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _paths.EnsureCreated();
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(
            stream,
            settings,
            GameSyncJsonContext.Default.UiSettings,
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved UI settings theme={Theme} onboarding={Onboarding}", settings.Theme, settings.OnboardingCompleted);
    }
}

using GameSync.Core.Abstractions.Configuration;
using Microsoft.UI.Xaml;

namespace GameSync.App.Services;

public interface IThemeService
{
    string CurrentTheme { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetThemeAsync(string theme, CancellationToken cancellationToken = default);
}

public sealed class ThemeService : IThemeService
{
    private readonly IUiSettingsStore _uiSettingsStore;

    public ThemeService(IUiSettingsStore uiSettingsStore)
    {
        _uiSettingsStore = uiSettingsStore;
    }

    public string CurrentTheme { get; private set; } = "System";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _uiSettingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        CurrentTheme = Normalize(settings.Theme);
        Apply(CurrentTheme);
    }

    public async Task SetThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        CurrentTheme = Normalize(theme);
        Apply(CurrentTheme);

        var settings = await _uiSettingsStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        settings.Theme = CurrentTheme;
        await _uiSettingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(true);
    }

    private static string Normalize(string? theme) =>
        theme is "Light" or "Dark" or "System" ? theme : "System";

    private static void Apply(string theme)
    {
        if (App.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}

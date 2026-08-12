namespace GameSync.Core.Models;

/// <summary>
/// Local UI preferences (theme, onboarding). Not synced through Git.
/// </summary>
public sealed class UiSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>System, Light, or Dark.</summary>
    public string Theme { get; set; } = "System";

    public bool OnboardingCompleted { get; set; }

    /// <summary>
    /// When true, GameSync checks GitHub Releases in the background shortly after startup.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;
}

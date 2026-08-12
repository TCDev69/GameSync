using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Models;
using GameSync.Core.Options;
using GameSync.Core.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GameSync.Infrastructure.Updates;

/// <summary>
/// Detects newer GitHub Releases and opens the Inno Setup installer download (HTTPS).
/// User data under %LOCALAPPDATA%\GameSync\ lives outside the install directory and is preserved across updates.
/// </summary>
public sealed class GitHubReleaseAppUpdateService : IAppUpdateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUriLauncher _uriLauncher;
    private readonly GameSyncOptions _options;
    private readonly ILogger<GitHubReleaseAppUpdateService> _logger;
    private AppUpdateCheckResult? _lastCheck;

    public GitHubReleaseAppUpdateService(
        IHttpClientFactory httpClientFactory,
        IUriLauncher uriLauncher,
        IOptions<GameSyncOptions> options,
        ILogger<GitHubReleaseAppUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _uriLauncher = uriLauncher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = AppVersion.Semantic;

        if (string.IsNullOrWhiteSpace(_options.UpdateReleasesOwner) || string.IsNullOrWhiteSpace(_options.UpdateReleasesRepo))
        {
            _lastCheck = AppUpdateCheckResult.None(current, "Update feed is not configured.");
            return _lastCheck;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GitHubApi");
            var path = $"repos/{_options.UpdateReleasesOwner}/{_options.UpdateReleasesRepo}/releases/latest";
            var release = await client.GetFromJsonAsync<GitHubReleaseDto>(path, cancellationToken).ConfigureAwait(false);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                _lastCheck = AppUpdateCheckResult.None(current, "No GitHub Releases found.");
                return _lastCheck;
            }

            if (!AppVersion.TryParseTag(release.TagName, out var latest))
            {
                _lastCheck = AppUpdateCheckResult.None(current, $"Unable to parse release tag '{release.TagName}'.");
                return _lastCheck;
            }

            var currentVersion = AppVersion.ParseSemantic();
            var setup = FindSetupAsset(release);
            var available = AppVersion.IsNewer(latest, currentVersion);

            _lastCheck = new AppUpdateCheckResult
            {
                UpdateAvailable = available,
                CurrentVersion = current,
                LatestVersion = latest.ToString(3),
                ReleaseNotesUrl = release.HtmlUrl,
                InstallerUri = setup,
                PackageDownloadUri = setup,
                Message = available
                    ? $"Version {latest.ToString(3)} is available."
                    : "You are on the latest version."
            };
            return _lastCheck;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Update check failed");
            _lastCheck = AppUpdateCheckResult.None(current, "Could not check for updates. Try again later.");
            return _lastCheck;
        }
    }

    public async Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        return result.UpdateAvailable;
    }

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        var check = _lastCheck ?? await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        if (!check.UpdateAvailable)
        {
            throw new InvalidOperationException(check.Message ?? "No update is available.");
        }

        var uriString = check.InstallerUri ?? check.PackageDownloadUri ?? check.ReleaseNotesUrl;
        if (string.IsNullOrWhiteSpace(uriString)
            || !Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Update is available but no secure HTTPS installer URI was found.");
        }

        if (!IsTrustedUpdateHost(uri.Host))
        {
            throw new InvalidOperationException(
                $"Update URI host '{uri.Host}' is not an allowed download host.");
        }

        _logger.LogInformation("Opening update URI via system handler (Setup.exe / browser)");
        await _uriLauncher.OpenAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTrustedUpdateHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static string? FindSetupAsset(GitHubReleaseDto release)
    {
        if (release.Assets is null || release.Assets.Count == 0)
        {
            return null;
        }

        static bool HasUrl(GitHubAssetDto a) => !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl);

        // Prefer the canonical Inno Setup artifact name.
        var preferred = release.Assets.FirstOrDefault(a =>
            HasUrl(a)
            && a.Name is not null
            && a.Name.StartsWith("GameSync-Setup", StringComparison.OrdinalIgnoreCase)
            && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (preferred is not null)
        {
            return preferred.BrowserDownloadUrl;
        }

        return release.Assets
            .FirstOrDefault(a =>
                HasUrl(a)
                && a.Name is not null
                && a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}

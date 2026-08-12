using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Models;
using GameSync.Core.Options;
using GameSync.Core.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace GameSync.Infrastructure.Updates;

/// <summary>
/// Self-update against GitHub Releases: finds the newest release, downloads the Inno Setup
/// installer over HTTPS, verifies it against the digest published with the asset, then runs it
/// unattended. User data under %LOCALAPPDATA%\GameSync\ lives outside the install directory and
/// is preserved across updates.
/// </summary>
public sealed class GitHubReleaseAppUpdateService : IAppUpdateService
{
    /// <summary>
    /// Inno Setup switches: show only the progress window, never prompt, close and restart GameSync.
    /// </summary>
    internal const string InstallerArguments =
        "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";

    internal const string DownloadClientName = "GitHubDownload";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUpdateInstallerLauncher _installerLauncher;
    private readonly ILocalAppDataPaths _paths;
    private readonly GameSyncOptions _options;
    private readonly ILogger<GitHubReleaseAppUpdateService> _logger;
    private AppUpdateCheckResult? _lastCheck;

    public GitHubReleaseAppUpdateService(
        IHttpClientFactory httpClientFactory,
        IUpdateInstallerLauncher installerLauncher,
        ILocalAppDataPaths paths,
        IOptions<GameSyncOptions> options,
        ILogger<GitHubReleaseAppUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _installerLauncher = installerLauncher;
        _paths = paths;
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
            var feed = $"{_options.UpdateReleasesOwner}/{_options.UpdateReleasesRepo}";
            var path = $"repos/{feed}/releases/latest";
            using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Update feed {Feed} was not found on GitHub", feed);
                _lastCheck = AppUpdateCheckResult.None(
                    current,
                    $"No GitHub Releases found for '{feed}'. "
                    + "If you use a fork, set GAMESYNC_UPDATE_OWNER to the correct GitHub username.");
                return _lastCheck;
            }

            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
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
                InstallerUri = setup?.BrowserDownloadUrl,
                PackageDownloadUri = setup?.BrowserDownloadUrl,
                InstallerFileName = setup?.Name,
                InstallerSizeBytes = setup?.Size > 0 ? setup.Size : null,
                InstallerSha256 = ParseSha256Digest(setup?.Digest),
                Message = available
                    ? $"Version {latest.ToString(3)} is available."
                    : "You are on the latest version."
            };

            _logger.LogInformation(
                "Update check: installed {Current}, latest {Latest}, available {Available}, installer {Installer}",
                current,
                latest.ToString(3),
                available,
                setup?.Name ?? "(none)");
            return _lastCheck;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            var feed = $"{_options.UpdateReleasesOwner}/{_options.UpdateReleasesRepo}";
            _logger.LogWarning(ex, "Update check failed for {Feed}", feed);
            _lastCheck = AppUpdateCheckResult.None(
                current,
                $"Could not check '{feed}' for updates. Verify your network connection and try again.");
            return _lastCheck;
        }
    }

    public async Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        return result.UpdateAvailable;
    }

    public async Task<AppUpdateInstallResult> UpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var check = _lastCheck ?? await CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        if (!check.UpdateAvailable)
        {
            throw new InvalidOperationException(check.Message ?? "No update is available.");
        }

        var uri = ResolveInstallerUri(check);
        var version = check.LatestVersion ?? "latest";
        var installerPath = await DownloadInstallerAsync(uri, check, version, progress, cancellationToken)
            .ConfigureAwait(false);

        int processId;
        try
        {
            processId = await _installerLauncher
                .StartAsync(installerPath, InstallerArguments, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The update was downloaded and verified, but could not be started. Run it manually: {installerPath}",
                ex);
        }

        _logger.LogInformation("Update to {Version} installing from {Path}", version, installerPath);
        return new AppUpdateInstallResult
        {
            InstallerStarted = true,
            Version = version,
            InstallerPath = installerPath,
            ProcessId = processId,
            ShouldExitApplication = true,
            Message = $"Installing GameSync {version}. The app closes so files can be replaced, then reopens."
        };
    }

    private static Uri ResolveInstallerUri(AppUpdateCheckResult check)
    {
        var uriString = check.InstallerUri ?? check.PackageDownloadUri;
        if (string.IsNullOrWhiteSpace(uriString)
            || !Uri.TryCreate(uriString, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The release does not publish a Setup.exe over HTTPS, so it cannot be installed automatically.");
        }

        if (!IsTrustedUpdateHost(uri.Host))
        {
            throw new InvalidOperationException(
                $"Update URI host '{uri.Host}' is not an allowed download host.");
        }

        return uri;
    }

    private async Task<string> DownloadInstallerAsync(
        Uri uri,
        AppUpdateCheckResult check,
        string version,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.UpdatesDirectory);
        var targetPath = Path.Combine(_paths.UpdatesDirectory, BuildInstallerFileName(check, version));
        var partialPath = targetPath + ".part";

        try
        {
            var client = _httpClientFactory.CreateClient(DownloadClientName);
            using var response = await client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var expectedTotal = response.Content.Headers.ContentLength ?? check.InstallerSizeBytes ?? 0;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(
                partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long written = 0;
                var lastReported = -1;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    written += read;

                    if (progress is null || expectedTotal <= 0)
                    {
                        continue;
                    }

                    var percent = (int)Math.Min(100, written * 100 / expectedTotal);
                    if (percent != lastReported)
                    {
                        lastReported = percent;
                        progress.Report(percent);
                    }
                }
            }

            VerifyInstaller(partialPath, check);
            File.Move(partialPath, targetPath, overwrite: true);
            progress?.Report(100);
            return targetPath;
        }
        catch
        {
            TryDelete(partialPath);
            throw;
        }
    }

    /// <summary>
    /// The installer is not code-signed, so the release digest is the only integrity guarantee.
    /// A mismatch means the download is corrupt or tampered with and must never be executed.
    /// </summary>
    private void VerifyInstaller(string path, AppUpdateCheckResult check)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0)
        {
            throw new InvalidOperationException("The downloaded update is empty.");
        }

        if (check.InstallerSizeBytes is > 0 && info.Length != check.InstallerSizeBytes)
        {
            throw new InvalidOperationException(
                $"The downloaded update is {info.Length} bytes but the release lists {check.InstallerSizeBytes}.");
        }

        if (!IsWindowsExecutable(path))
        {
            throw new InvalidOperationException("The downloaded update is not a Windows executable.");
        }

        if (string.IsNullOrWhiteSpace(check.InstallerSha256))
        {
            _logger.LogWarning("Release does not publish a digest; installing after size and format checks only");
            return;
        }

        var actual = ComputeSha256(path);
        if (!actual.Equals(check.InstallerSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The downloaded update failed its SHA-256 check and was discarded.");
        }

        _logger.LogInformation("Update payload verified (sha256 {Digest})", actual);
    }

    private static string BuildInstallerFileName(AppUpdateCheckResult check, string version)
    {
        var name = check.InstallerFileName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var candidate = Path.GetFileName(name.Trim());
            if (candidate.Length > 0
                && candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && candidate.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            {
                return $"GameSync-{version}-{candidate}";
            }
        }

        return $"GameSync-Setup-{version}.exe";
    }

    private static bool IsWindowsExecutable(string path)
    {
        using var stream = File.OpenRead(path);
        return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? ParseSha256Digest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        var value = digest.Trim();
        const string prefix = "sha256:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..];
        }
        else if (value.Contains(':', StringComparison.Ordinal))
        {
            // Some other hash algorithm; we only know how to verify SHA-256.
            return null;
        }

        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsTrustedUpdateHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static GitHubAssetDto? FindSetupAsset(GitHubReleaseDto release)
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
            return preferred;
        }

        return release.Assets.FirstOrDefault(a =>
            HasUrl(a)
            && a.Name is not null
            && a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
            && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
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

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}

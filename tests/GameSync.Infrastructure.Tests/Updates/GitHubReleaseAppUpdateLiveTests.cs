using FluentAssertions;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Options;
using GameSync.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.Updates;

/// <summary>
/// Exercises the real update path against published GitHub Releases: live API call, installer
/// download through GitHub's redirect, and SHA-256 verification. The installer is never executed.
///
/// Opt-in because it needs the network and downloads ~75 MB:
///   $env:GAMESYNC_LIVE_UPDATE_TEST = '1'
///   dotnet test tests/GameSync.Infrastructure.Tests -p:Version=0.9.0 --filter FullyQualifiedName~GitHubReleaseAppUpdateLiveTests
///
/// The version override makes the published release look newer than the build under test.
/// </summary>
[Trait("Category", "Live")]
public sealed class GitHubReleaseAppUpdateLiveTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "GameSyncLiveUpdateTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LatestRelease_PublishesAVerifiableInstaller()
    {
        if (!IsEnabled)
        {
            return;
        }

        var result = await CreateService(out _).CheckForUpdatesAsync();

        result.LatestVersion.Should().NotBeNullOrWhiteSpace();
        result.InstallerUri.Should().StartWith("https://github.com/");
        result.InstallerFileName.Should().EndWith(".exe");
        result.InstallerSizeBytes.Should().BeGreaterThan(0);
        result.InstallerSha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Update_DownloadsAndVerifiesTheRealInstaller()
    {
        if (!IsEnabled)
        {
            return;
        }

        var service = CreateService(out var launcher);
        var check = await service.CheckForUpdatesAsync();
        check.UpdateAvailable.Should().BeTrue(
            "the published release {0} must look newer than the build under test {1} — rerun with -p:Version=0.9.0",
            check.LatestVersion,
            check.CurrentVersion);

        var reported = new List<int>();
        var result = await service.UpdateAsync(new Progress<int>(reported.Add));

        result.InstallerStarted.Should().BeTrue();
        new FileInfo(result.InstallerPath!).Length.Should().Be(check.InstallerSizeBytes);
        reported.Should().Contain(100);

        await launcher.Received(1).StartAsync(
            result.InstallerPath!,
            Arg.Is<string>(a => a.Contains("/SILENT")),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static bool IsEnabled =>
        Environment.GetEnvironmentVariable("GAMESYNC_LIVE_UPDATE_TEST") == "1";

    private GitHubReleaseAppUpdateService CreateService(out IUpdateInstallerLauncher launcher)
    {
        launcher = Substitute.For<IUpdateInstallerLauncher>();
        launcher.StartAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        var options = new GameSyncOptions();
        return new GitHubReleaseAppUpdateService(
            new LiveHttpClientFactory(options),
            launcher,
            new LivePaths(_root),
            Options.Create(options),
            NullLogger<GitHubReleaseAppUpdateService>.Instance);
    }

    private sealed class LiveHttpClientFactory : IHttpClientFactory
    {
        private readonly GameSyncOptions _options;

        public LiveHttpClientFactory(GameSyncOptions options)
        {
            _options = options;
        }

        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameSync");
            if (name == "GitHubApi")
            {
                client.BaseAddress = new Uri(_options.GitHubApiBaseUrl);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            }

            return client;
        }
    }

    private sealed class LivePaths : ILocalAppDataPaths
    {
        public LivePaths(string root)
        {
            Root = root;
            MachineConfigurationFile = Path.Combine(root, "machine.json");
            RepositoriesDirectory = Path.Combine(root, "repositories");
            CacheDirectory = Path.Combine(root, "cache");
            LogsDirectory = Path.Combine(root, "logs");
            BackupsDirectory = Path.Combine(root, "backups");
            UpdatesDirectory = Path.Combine(root, "updates");
        }

        public string Root { get; }
        public string MachineConfigurationFile { get; }
        public string RepositoriesDirectory { get; }
        public string CacheDirectory { get; }
        public string LogsDirectory { get; }
        public string BackupsDirectory { get; }
        public string UpdatesDirectory { get; }

        public void EnsureCreated() => Directory.CreateDirectory(UpdatesDirectory);
    }
}

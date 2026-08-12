using FluentAssertions;
using GameSync.Core.Abstractions.Storage;
using GameSync.Infrastructure.Git;
using GameSync.Infrastructure.Repositories;
using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.Git;

public sealed class GitServiceTests
{
    [Fact]
    public async Task CloneAddCommitStatusHistory_WorksAgainstLocalRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncGitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            var work = Path.Combine(root, "work");
            SeedRepository(source);

            var credentials = Substitute.For<ICredentialStore>();
            credentials.RetrieveSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns((string?)null);

            var git = new GitService(credentials, NullLogger<GitService>.Instance);
            await git.CloneAsync(source, work);

            Directory.CreateDirectory(Path.Combine(work, "saves", "demo"));
            File.WriteAllText(Path.Combine(work, "saves", "demo", "a.sav"), "1");

            await git.AddAsync(work, ["saves/demo"]);
            await git.CommitAsync(work, "GameSync: Update Demo saves");

            var status = await git.GetStatusAsync(work);
            status.HasUncommittedChanges.Should().BeFalse();
            status.HeadCommitSha.Should().NotBeNullOrWhiteSpace();

            var history = await git.GetHistoryAsync(work);
            history.Should().NotBeEmpty();
            history.Any(h => h.Message.Contains("GameSync", StringComparison.Ordinal)).Should().BeTrue();

            await git.FetchAsync(work);
            var conflicts = await git.GetConflictsAsync(work);
            conflicts.Should().BeEmpty();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void RepositoryService_UsesDeterministicPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncRepoSvc", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new TestPaths(root);
            paths.EnsureCreated();
            var service = new RepositoryService(paths, Substitute.For<GameSync.Core.Abstractions.Git.IGitService>(), NullLogger<RepositoryService>.Instance);
            var local = service.GetLocalRepositoryPath("Octo", "saves-repo");
            local.Should().Be(Path.Combine(paths.RepositoriesDirectory, "Octo__saves-repo"));
            service.BuildCloneUrl(new GameSync.Core.Models.RepositoryConfiguration
            {
                Owner = "Octo",
                Name = "saves-repo"
            }).Should().Be("https://github.com/Octo/saves-repo.git");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void SeedRepository(string path)
    {
        Directory.CreateDirectory(path);
        Repository.Init(path);
        File.WriteAllText(Path.Combine(path, "README.md"), "seed");
        using var repo = new Repository(path);
        Commands.Stage(repo, "*");
        var signature = new Signature("GameSync", "gamesSync@test.local", DateTimeOffset.Now);
        repo.Commit("seed", signature, signature);
    }

    private static void TryDelete(string root)
    {
        for (var i = 0; i < 8; i++)
        {
            try
            {
                Directory.Delete(root, true);
                return;
            }
            catch
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class TestPaths : ILocalAppDataPaths
    {
        public TestPaths(string root)
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
        public void EnsureCreated()
        {
            Directory.CreateDirectory(RepositoriesDirectory);
            Directory.CreateDirectory(BackupsDirectory);
        }
    }
}

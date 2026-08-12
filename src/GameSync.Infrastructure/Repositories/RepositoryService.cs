using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Repository;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Repositories;

public sealed class RepositoryService : IRepositoryService
{
    private readonly ILocalAppDataPaths _paths;
    private readonly IGitService _gitService;
    private readonly ILogger<RepositoryService> _logger;

    public RepositoryService(ILocalAppDataPaths paths, IGitService gitService, ILogger<RepositoryService> logger)
    {
        _paths = paths;
        _gitService = gitService;
        _logger = logger;
    }

    public string GetLocalRepositoryPath(string owner, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _paths.EnsureCreated();

        var folder = $"{Sanitize(owner)}__{Sanitize(name)}";
        return Path.Combine(_paths.RepositoriesDirectory, folder);
    }

    public async Task<RepositoryConfiguration> EnsureLocalRepositoryAsync(RepositoryConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration.Owner) || string.IsNullOrWhiteSpace(configuration.Name))
        {
            throw new RepositoryUnavailableException("Repository owner and name are required.");
        }

        var localPath = string.IsNullOrWhiteSpace(configuration.LocalPath)
            ? GetLocalRepositoryPath(configuration.Owner, configuration.Name)
            : configuration.LocalPath;

        if (await IsLocalRepositoryReadyAsync(localPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Using existing local repository at {LocalPath}", localPath);
            return configuration with { LocalPath = localPath };
        }

        var cloneUrl = BuildCloneUrl(configuration)
            ?? throw new RepositoryUnavailableException("Clone URL could not be determined.");

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await _gitService.CloneAsync(cloneUrl, localPath, cancellationToken).ConfigureAwait(false);

        return configuration with { LocalPath = localPath, CloneUrl = cloneUrl };
    }

    public Task<bool> IsLocalRepositoryReadyAsync(string localPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(LibGit2Sharp.Repository.IsValid(localPath));
    }

    public string? BuildCloneUrl(RepositoryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!string.IsNullOrWhiteSpace(configuration.CloneUrl))
        {
            return configuration.CloneUrl;
        }

        if (string.IsNullOrWhiteSpace(configuration.Owner) || string.IsNullOrWhiteSpace(configuration.Name))
        {
            return null;
        }

        return $"https://github.com/{configuration.Owner}/{configuration.Name}.git";
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value.Trim();
    }
}

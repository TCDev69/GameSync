using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameSync.Infrastructure.Configuration;

public sealed class JsonSharedGamesConfigurationStore : ISharedGamesConfigurationStore
{
    public const string RelativeConfigurationPath = "config/games.json";

    private readonly IConfigurationValidator _validator;
    private readonly ILogger<JsonSharedGamesConfigurationStore> _logger;

    public JsonSharedGamesConfigurationStore(
        IConfigurationValidator validator,
        ILogger<JsonSharedGamesConfigurationStore> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public string GetConfigurationRelativePath() => RelativeConfigurationPath;

    public async Task<GamesConfiguration> LoadAsync(string repositoryLocalPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);

        var path = GetAbsolutePath(repositoryLocalPath);
        if (!File.Exists(path))
        {
            return new GamesConfiguration();
        }

        await using var stream = File.OpenRead(path);
        var configuration = await JsonSerializer.DeserializeAsync(
                stream,
                GameSyncJsonContext.Default.GamesConfiguration,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("games.json deserialized to null.");

        var errors = _validator.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }

        return configuration;
    }

    public async Task SaveAsync(string repositoryLocalPath, GamesConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);
        ArgumentNullException.ThrowIfNull(configuration);

        var errors = _validator.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }

        var path = GetAbsolutePath(repositoryLocalPath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            configuration,
            GameSyncJsonContext.Default.GamesConfiguration,
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved shared games configuration with {GameCount} game(s)", configuration.Games.Count);
    }

    private static string GetAbsolutePath(string repositoryLocalPath) =>
        Path.Combine(repositoryLocalPath, RelativeConfigurationPath.Replace('/', Path.DirectorySeparatorChar));
}

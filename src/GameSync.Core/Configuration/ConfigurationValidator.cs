using System.Text.RegularExpressions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Models;
using GameSync.Core.Services;

namespace GameSync.Core.Configuration;

public sealed class ConfigurationValidator : IConfigurationValidator
{
    private static readonly Regex GameIdRegex = new("^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyList<string> Validate(GamesConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<string>();

        if (configuration.SchemaVersion < 1)
        {
            errors.Add("games.json schemaVersion must be >= 1.");
        }

        var seenGameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in configuration.Games)
        {
            if (string.IsNullOrWhiteSpace(game.Id))
            {
                errors.Add("Each game requires a non-empty id.");
                continue;
            }

            if (!GameIdRegex.IsMatch(game.Id))
            {
                errors.Add($"Game id '{game.Id}' must be lowercase alphanumeric with underscores.");
            }

            if (!seenGameIds.Add(game.Id))
            {
                errors.Add($"Duplicate game id '{game.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(game.Title))
            {
                errors.Add($"Game '{game.Id}' requires a title.");
            }

            if (game.SaveLocations.Count == 0)
            {
                errors.Add($"Game '{game.Id}' requires at least one save location.");
            }

            var seenSaveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var save in game.SaveLocations)
            {
                if (string.IsNullOrWhiteSpace(save.Id))
                {
                    errors.Add($"Game '{game.Id}' has a save location without an id.");
                    continue;
                }

                if (!seenSaveIds.Add(save.Id))
                {
                    errors.Add($"Game '{game.Id}' has duplicate save location id '{save.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(save.LocalPath))
                {
                    errors.Add($"Game '{game.Id}' save '{save.Id}' requires localPath.");
                }

                if (string.IsNullOrWhiteSpace(save.RemotePath))
                {
                    errors.Add($"Game '{game.Id}' save '{save.Id}' requires remotePath.");
                }
                else if (Path.IsPathRooted(save.RemotePath) || save.RemotePath.Contains("..", StringComparison.Ordinal))
                {
                    errors.Add($"Game '{game.Id}' save '{save.Id}' remotePath must be a relative repository path.");
                }
            }
        }

        return errors;
    }

    public IReadOnlyList<string> Validate(MachineConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<string>();

        if (configuration.SchemaVersion < 1)
        {
            errors.Add("machine.json schemaVersion must be >= 1.");
        }

        if (string.IsNullOrWhiteSpace(configuration.MachineId))
        {
            errors.Add("machineId is required.");
        }

        if (configuration.Backup.MaxBackupsPerGame < 0)
        {
            errors.Add("backup.maxBackupsPerGame must be >= 0.");
        }

        foreach (var (gameId, launch) in configuration.Games)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                errors.Add("Machine game keys must be non-empty.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(launch.Executable))
            {
                errors.Add($"Machine launch config for '{gameId}' requires an executable path.");
            }
            else if (!LaunchTarget.IsProtocolUri(launch.Executable)
                     && launch.Executable.Contains("..", StringComparison.Ordinal))
            {
                errors.Add($"Machine launch config for '{gameId}' executable must not contain '..'.");
            }
        }

        if (configuration.Repository is { } repo)
        {
            if (string.IsNullOrWhiteSpace(repo.Owner) || string.IsNullOrWhiteSpace(repo.Name))
            {
                errors.Add("Repository owner and name are required when repository is configured.");
            }
        }

        return errors;
    }
}

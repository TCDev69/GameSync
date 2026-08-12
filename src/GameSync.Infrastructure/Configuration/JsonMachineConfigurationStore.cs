using System.Text.Json;
using System.Text.Json.Serialization;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Configuration;

public sealed class JsonMachineConfigurationStore : IMachineConfigurationStore
{
    private readonly ILocalAppDataPaths _paths;
    private readonly IConfigurationValidator _validator;
    private readonly ILogger<JsonMachineConfigurationStore> _logger;

    internal static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public JsonMachineConfigurationStore(
        ILocalAppDataPaths paths,
        IConfigurationValidator validator,
        ILogger<JsonMachineConfigurationStore> logger)
    {
        _paths = paths;
        _validator = validator;
        _logger = logger;
    }

    public async Task<MachineConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.MachineConfigurationFile))
        {
            var created = new MachineConfiguration
            {
                MachineId = Environment.MachineName
            };
            await SaveAsync(created, cancellationToken).ConfigureAwait(false);
            return created;
        }

        await using var stream = File.OpenRead(_paths.MachineConfigurationFile);
        var configuration = await JsonSerializer.DeserializeAsync<MachineConfiguration>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("machine.json deserialized to null.");

        var errors = _validator.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }

        return configuration;
    }

    public async Task SaveAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _paths.EnsureCreated();

        var errors = _validator.Validate(configuration);
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(errors);
        }

        var directory = Path.GetDirectoryName(_paths.MachineConfigurationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_paths.MachineConfigurationFile);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved machine configuration for {MachineId}", configuration.MachineId);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

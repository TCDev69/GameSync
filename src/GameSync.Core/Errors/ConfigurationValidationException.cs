namespace GameSync.Core.Errors;

public sealed class ConfigurationValidationException : GameSyncException
{
    public IReadOnlyList<string> Errors { get; }

    public ConfigurationValidationException(IReadOnlyList<string> errors)
        : base("ConfigurationValidationFailed", FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IReadOnlyList<string> errors) =>
        errors.Count == 0
            ? "Configuration validation failed."
            : string.Join(Environment.NewLine, errors);
}

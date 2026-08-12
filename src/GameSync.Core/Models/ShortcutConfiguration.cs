namespace GameSync.Core.Models;

/// <summary>
/// Describes a shortcut that launches GameSync with --game &lt;id&gt;.
/// </summary>
public sealed class ShortcutConfiguration
{
    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    public required ShortcutKind Kind { get; init; }

    public string? IconPath { get; init; }

    public string? Description { get; init; }
}

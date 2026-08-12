namespace GameSync.Core.Commands;

/// <summary>
/// Parses GameSync CLI arguments. Kept out of the UI layer so headless workflows can reuse it.
/// </summary>
public static class AppCommandParser
{
    public static AppCommand Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return new AppCommand { Kind = AppCommandKind.Dashboard, RawArguments = args };
        }

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (IsSwitch(token, "help") || token is "-?" or "/?")
            {
                return new AppCommand { Kind = AppCommandKind.Help, RawArguments = args };
            }

            if (IsSwitch(token, "game"))
            {
                var gameId = RequireValue(args, i, "--game");
                return new AppCommand
                {
                    Kind = AppCommandKind.LaunchGame,
                    GameId = gameId,
                    RawArguments = args
                };
            }

            if (IsSwitch(token, "sync"))
            {
                string? gameId = null;
                if (i + 1 < args.Count && !IsSwitchToken(args[i + 1]))
                {
                    gameId = args[i + 1];
                }

                return new AppCommand
                {
                    Kind = string.IsNullOrWhiteSpace(gameId) ? AppCommandKind.SyncAll : AppCommandKind.SyncGame,
                    GameId = gameId,
                    RawArguments = args
                };
            }

            if (IsSwitch(token, "status"))
            {
                return new AppCommand { Kind = AppCommandKind.Status, RawArguments = args };
            }

            if (IsSwitch(token, "settings"))
            {
                return new AppCommand { Kind = AppCommandKind.Settings, RawArguments = args };
            }
        }

        throw new ArgumentException($"Unrecognized command-line arguments: {string.Join(' ', args)}");
    }

    public static string GetHelpText() =>
        """
        GameSync — synchronize and launch games via your private GitHub repository.

        Usage:
          GameSync.exe
          GameSync.exe --game <game-id>
          GameSync.exe --sync
          GameSync.exe --sync <game-id>
          GameSync.exe --status
          GameSync.exe --settings
          GameSync.exe --help

        Options:
          --game <game-id>   Sync saves, launch the game, then sync on exit
          --sync [game-id]   Synchronize all games or a single game
          --status           Show sync status
          --settings         Show machine and repository settings
          --help             Show this help

        Headless mode:
          Commands above run in the console without opening the GameSync window.
          Use --game with a Steam URI or drag the game .exe when configuring launch.
        """;

    private static bool IsSwitchToken(string token) =>
        token.StartsWith('-') || token.StartsWith('/');

    private static bool IsSwitch(string token, string name) =>
        token.Equals($"--{name}", StringComparison.OrdinalIgnoreCase)
        || token.Equals($"-{name}", StringComparison.OrdinalIgnoreCase)
        || token.Equals($"/{name}", StringComparison.OrdinalIgnoreCase);

    private static string RequireValue(IReadOnlyList<string> args, int switchIndex, string switchName)
    {
        if (switchIndex + 1 >= args.Count || IsSwitchToken(args[switchIndex + 1]))
        {
            throw new ArgumentException($"The {switchName} switch requires a value.");
        }

        var value = args[switchIndex + 1].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The {switchName} switch requires a non-empty value.");
        }

        return value;
    }
}

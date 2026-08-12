using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameSync.Infrastructure.Logging;

public sealed class GameSyncFileLoggerOptions
{
    public string? LogsDirectory { get; set; }

    public string FilePrefix { get; set; } = "gameSync";

    /// <summary>Minimum level written to disk.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Delete log files older than this many days. 0 disables pruning.</summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Soft size hint per daily file before a rollover suffix is used (bytes).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
}

public sealed class GameSyncFileLoggerProvider : ILoggerProvider
{
    private readonly string _logsDirectory;
    private readonly string _filePrefix;
    private readonly LogLevel _minimumLevel;
    private readonly int _retentionDays;
    private readonly long _maxFileSizeBytes;
    private readonly object _gate = new();

    public GameSyncFileLoggerProvider(IOptions<GameSyncFileLoggerOptions> options)
    {
        var value = options.Value;
        _logsDirectory = value.LogsDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameSync", "logs");
        _filePrefix = string.IsNullOrWhiteSpace(value.FilePrefix) ? "gameSync" : value.FilePrefix;
        _minimumLevel = value.MinimumLevel;
        _retentionDays = Math.Max(0, value.RetentionDays);
        _maxFileSizeBytes = value.MaxFileSizeBytes <= 0 ? 5 * 1024 * 1024 : value.MaxFileSizeBytes;
        Directory.CreateDirectory(_logsDirectory);
        PruneOldLogs();
    }

    public ILogger CreateLogger(string categoryName) => new GameSyncFileLogger(categoryName, this);

    public void Dispose()
    {
    }

    internal bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= _minimumLevel;

    internal void Write(string categoryName, LogLevel logLevel, string message, Exception? exception)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        message = Redact(message);
        var exceptionText = exception is null ? null : Redact(exception.ToString());

        var line = $"{DateTimeOffset.Now:O} [{logLevel}] {categoryName}: {message}";
        if (exceptionText is not null)
        {
            line += Environment.NewLine + exceptionText;
        }

        lock (_gate)
        {
            var filePath = ResolveLogFilePath();
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }

    private string ResolveLogFilePath()
    {
        var day = DateTime.UtcNow.ToString("yyyyMMdd");
        var primary = Path.Combine(_logsDirectory, $"{_filePrefix}-{day}.log");
        if (!File.Exists(primary))
        {
            return primary;
        }

        try
        {
            var info = new FileInfo(primary);
            if (info.Length < _maxFileSizeBytes)
            {
                return primary;
            }
        }
        catch
        {
            return primary;
        }

        // Size-based rollover within the same day.
        for (var i = 1; i < 100; i++)
        {
            var rolled = Path.Combine(_logsDirectory, $"{_filePrefix}-{day}.{i}.log");
            if (!File.Exists(rolled) || new FileInfo(rolled).Length < _maxFileSizeBytes)
            {
                return rolled;
            }
        }

        return primary;
    }

    private void PruneOldLogs()
    {
        if (_retentionDays <= 0)
        {
            return;
        }

        try
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_logsDirectory, $"{_filePrefix}-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file).Date < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Best-effort retention.
                }
            }
        }
        catch
        {
            // Ignore prune failures at startup.
        }
    }

    private static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        if (ContainsSecretMarker(message))
        {
            return "[REDACTED]";
        }

        return message;
    }

    private static bool ContainsSecretMarker(string message) =>
        message.Contains("access_token", StringComparison.OrdinalIgnoreCase)
        || message.Contains("refresh_token", StringComparison.OrdinalIgnoreCase)
        || message.Contains("client_secret", StringComparison.OrdinalIgnoreCase)
        || message.Contains("password", StringComparison.OrdinalIgnoreCase)
        || message.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
        || message.Contains("gho_", StringComparison.OrdinalIgnoreCase)
        || message.Contains("ghp_", StringComparison.OrdinalIgnoreCase)
        || message.Contains("ghu_", StringComparison.OrdinalIgnoreCase)
        || message.Contains("ghs_", StringComparison.OrdinalIgnoreCase)
        || message.Contains("github_pat_", StringComparison.OrdinalIgnoreCase);
}

internal sealed class GameSyncFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly GameSyncFileLoggerProvider _provider;

    public GameSyncFileLogger(string categoryName, GameSyncFileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        _provider.Write(_categoryName, logLevel, formatter(state, exception), exception);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}

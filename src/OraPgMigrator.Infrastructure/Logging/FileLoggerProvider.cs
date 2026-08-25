using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OraPgMigrator.Infrastructure.Logging;

/// <summary>
/// Minimal thread-safe plain-text file logger. Never writes secret values —
/// callers are responsible for keeping passwords out of anything passed to
/// <see cref="ILogger"/> (connection settings expose a masked ToString()).
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void WriteLine(string line)
    {
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {_category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }
            _provider.WriteLine(line);
        }
    }
}

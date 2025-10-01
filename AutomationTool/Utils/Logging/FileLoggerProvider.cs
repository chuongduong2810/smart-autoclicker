using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AutomationTool.Utils.Logging;

/// <summary>
/// Very lightweight file logger that writes log entries to a rolling text file.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _fileWriteLock = new();
    private bool _disposed;

    public FileLoggerProvider(string filePath, LogLevel minimumLevel = LogLevel.Information)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be provided", nameof(filePath));
        }

        _filePath = filePath;
        _minimumLevel = minimumLevel;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WritePreamble();
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FileLoggerProvider));
        }

        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _filePath, _fileWriteLock, _minimumLevel));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _loggers.Clear();
        _disposed = true;
    }

    private void WritePreamble()
    {
        var message = $"=== Logging session started {DateTimeOffset.Now:O} ==={Environment.NewLine}";
        lock (_fileWriteLock)
        {
            File.AppendAllText(_filePath, message);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _writeLock;
        private readonly LogLevel _minimumLevel;

        public FileLogger(string categoryName, string filePath, object writeLock, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _filePath = filePath;
            _writeLock = writeLock;
            _minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception == null)
            {
                return;
            }

            var logEntry = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {message}";
            if (exception != null)
            {
                logEntry += $"{Environment.NewLine}{exception}";
            }

            lock (_writeLock)
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
        }
    }
}


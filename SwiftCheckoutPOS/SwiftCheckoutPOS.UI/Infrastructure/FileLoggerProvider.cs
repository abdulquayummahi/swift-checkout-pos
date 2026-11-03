using System.Collections.Concurrent;
using System.Text;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// BP-06: local rolling log files (one per day, newest 30 kept) in DataRoot\Logs. Offline only: nothing is sent
/// anywhere. Messages are written as given; code must never log passwords, connection strings or costs.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _folder;
    private readonly int _keepDays;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private DateTime _day;
    private StreamWriter? _writer;

    public FileLoggerProvider(string folder, int keepDays = 30)
    {
        _folder = folder;
        _keepDays = keepDays;
        Directory.CreateDirectory(folder);
    }

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ')
            .Append(level.ToString().ToUpperInvariant()[..4]).Append(' ')
            .Append(category).Append(": ").Append(message.ReplaceLineEndings(" "));
        if (exception is not null) line.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message.ReplaceLineEndings(" "));
        lock (_gate)
        {
            try
            {
                if (_writer is null || _day != now.Date)
                {
                    _writer?.Dispose();
                    _day = now.Date;
                    _writer = new StreamWriter(new FileStream(Path.Combine(_folder, $"swiftcheckout-{now:yyyyMMdd}.log"),
                        FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8) { AutoFlush = true };
                    foreach (var old in new DirectoryInfo(_folder).GetFiles("swiftcheckout-*.log")
                                 .OrderByDescending(f => f.Name).Skip(_keepDays))
                        try { old.Delete(); } catch (IOException) { }
                }
                _writer.WriteLine(line.ToString());
            }
            catch (IOException) { /* logging must never stop a sale */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void Dispose()
    {
        lock (_gate) { _writer?.Dispose(); _writer = null; }
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}

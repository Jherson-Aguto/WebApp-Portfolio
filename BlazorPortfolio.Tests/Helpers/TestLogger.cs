using Microsoft.Extensions.Logging;

namespace BlazorPortfolio.Tests.Helpers;

/// <summary>
/// A simple in-memory logger that captures log entries for assertions.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add((logLevel, formatter(state, exception)));
    }

    public bool HasWarning(string containing) =>
        _entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains(containing));

    public bool HasCritical(string containing) =>
        _entries.Any(e => e.Level == LogLevel.Critical && e.Message.Contains(containing));
}

using Microsoft.Extensions.Logging;

namespace ScreenRecorder.Tests.Fakes;

/// <summary>Captures log entries so tests can assert what was reported.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, exception, formatter(state, exception)));
}

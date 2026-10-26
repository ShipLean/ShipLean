using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ShipLean.WebApi.Tests.Search.Ranking;

// An ILogger<T> that keeps the formatted messages, so a test can count what one query logged. Registered as the
// ILogger<T> itself because the app's Serilog logger factory ignores added ILoggerProviders.
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Lines => [.. _lines];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _lines.Enqueue((logLevel, formatter(state, exception)));
}

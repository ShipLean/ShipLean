using Microsoft.Extensions.Logging;

namespace ShipLean.WebApi.Tests.Backups.Job;

// Keeps every entry with its rendered message and the full text of its exception (ToString, as a log sink writes it), so a
// test can check what would reach Seq or OTLP.
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly object _gate = new();
    private readonly List<(LogLevel Level, string Message, string? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, string? Exception)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add((logLevel, formatter(state, exception), exception?.ToString()));
        }
    }
}

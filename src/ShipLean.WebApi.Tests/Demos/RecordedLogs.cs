using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ShipLean.WebApi.Tests.Demos;

// Stands in for the host's ILoggerFactory (the app's Serilog one ignores added providers) and keeps every event as one string: category, message,
// each structured property, each scope in force and the exception. It filters as appsettings.json does (Microsoft.AspNetCore and
// System.Net.Http.HttpClient at Warning, the rest Information), so what it keeps is what production would write.
internal sealed class RecordedLogs : ILoggerFactory
{
    private readonly ConcurrentQueue<string> _events = new();
    private readonly AsyncLocal<Scope?> _scope = new();

    public IReadOnlyList<string> Events => [.. _events];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);

    public void Dispose()
    {
    }

    private sealed record Scope(string Text, Scope? Parent);

    private sealed class Recorder(RecordedLogs owner, string category) : ILogger
    {
    private static LogLevel MinimumFor(string category)
        => category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal)
            ? LogLevel.Warning
            : LogLevel.Information;

    private static string Flatten(object? state)
        => state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(' ', pairs.Select(p => $"{p.Key}={p.Value}")) + " " + state
            : state?.ToString() ?? string.Empty;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            var parent = owner._scope.Value;
            owner._scope.Value = new Scope(Flatten(state), parent);
            return new Restore(owner, parent);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumFor(category);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var scopes = new List<string>();
            for (var scope = owner._scope.Value; scope is not null; scope = scope.Parent)
            {
                scopes.Add(scope.Text);
            }

            owner._events.Enqueue($"{logLevel} {category}: {formatter(state, exception)} | {Flatten(state)} | scopes: {string.Join(" ; ", scopes)} | {exception}");
        }
    }

    private sealed class Restore(RecordedLogs owner, Scope? parent) : IDisposable
    {
        public void Dispose() => owner._scope.Value = parent;
    }
}

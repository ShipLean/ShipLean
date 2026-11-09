using Serilog.Core;
using Serilog.Events;

namespace ShipLean.UnitTest.Logging;

// A Serilog sink that keeps every event it is given, for asserting on the properties an event ends up with.
public sealed class RecordedLogEvents : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Add(logEvent);
}

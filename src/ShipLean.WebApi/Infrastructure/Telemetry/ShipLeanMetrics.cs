using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Telemetry;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// Every instrument of the ShipLean meter, in one place. Tags stay bounded: a tool name from the registered tools, an outcome, a source
// kind. Never an item key, a user id, a project or free text.
public sealed class ShipLeanMetrics : IJobMetrics
{
    private const string ImportSource = "jira";

    private readonly Meter _meter = new(TelemetryNames.Meter);
    private readonly Counter<long> _toolCalls;
    private readonly Histogram<double> _toolDuration;
    private readonly Counter<long> _importJobs;
    private readonly Counter<long> _importItems;
    private readonly Counter<long> _searchIndexed;
    private int _indexQueue;

    public ShipLeanMetrics(IServiceProvider services)
    {
        _toolCalls = _meter.CreateCounter<long>("shiplean.mcp.tool_calls", description: "MCP tool calls, by tool and outcome.");
        _toolDuration = _meter.CreateHistogram<double>("shiplean.mcp.tool_duration", unit: "s", description: "How long an MCP tool call took.");
        _importJobs = _meter.CreateCounter<long>("shiplean.import.jobs", description: "Import jobs that ended, by source and outcome.");
        _importItems = _meter.CreateCounter<long>("shiplean.import.items", description: "Work items written by imports.");
        _searchIndexed = _meter.CreateCounter<long>("shiplean.search.indexed", description: "Search rows embedded.");
        _meter.CreateObservableGauge("shiplean.search.index_queue", () => Volatile.Read(ref _indexQueue), description: "Search rows still waiting for a vector after the last pass.");
        _meter.CreateObservableGauge("shiplean.realtime.presence_screens", () => services.GetService<IPresenceTracker>()?.ScreenCount ?? 0, description: "Screens with someone on them, as this instance sees them.");
    }

    public void ToolCalled(string tool, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList { { "tool", tool }, { "outcome", outcome } };
        _toolCalls.Add(1, tags);
        _toolDuration.Record(elapsed.TotalSeconds, tags);
    }

    public void ImportJobFinished(string outcome) => _importJobs.Add(1, new TagList { { "source", ImportSource }, { "outcome", outcome } });

    public void ImportItemsWritten(int count) => _importItems.Add(count, new TagList { { "source", ImportSource } });

    public void SearchPassFinished(int indexed, int remaining)
    {
        _searchIndexed.Add(indexed);
        Volatile.Write(ref _indexQueue, remaining);
    }
}

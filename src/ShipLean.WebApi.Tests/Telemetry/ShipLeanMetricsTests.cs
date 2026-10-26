using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Telemetry;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Telemetry;

[TestFixture]
[NonParallelizable]
public class ShipLeanMetricsTests : McpFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private string? _previous;

    protected override Task BeforeHostStartAsync()
    {
        _previous = Environment.GetEnvironmentVariable(EndpointKey);
        Environment.SetEnvironmentVariable(EndpointKey, "http://127.0.0.1:1");
        return Task.CompletedTask;
    }

    protected override Task AfterHostStopAsync()
    {
        Environment.SetEnvironmentVariable(EndpointKey, _previous);
        return Task.CompletedTask;
    }

    [Test]
    public async Task ToolCall_WhenToolSucceeds_ShouldCountCallWithToolAndOutcome()
    {
        var recorded = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = Listen(recorded);
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "list_projects");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var call = recorded.Single(r => r.Name == "shiplean.mcp.tool_calls");
        call.Value.ShouldBe(1);
        call.Tags["tool"].ShouldBe("list_projects");
        call.Tags["outcome"].ShouldBe("ok");
        recorded.ShouldContain(r => r.Name == "shiplean.mcp.tool_duration");
    }

    [Test]
    public void ImportJobFinished_WhenJobFails_ShouldCountJobBySourceAndOutcome()
    {
        var recorded = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = Listen(recorded);
        var metrics = Host.Services.GetRequiredService<IJobMetrics>();

        metrics.ImportJobFinished("failed");
        metrics.ImportItemsWritten(7);

        var job = recorded.Single(r => r.Name == "shiplean.import.jobs");
        job.Tags["source"].ShouldBe("jira");
        job.Tags["outcome"].ShouldBe("failed");
        recorded.Single(r => r.Name == "shiplean.import.items").Value.ShouldBe(7);
    }

    [Test]
    public void SearchPassFinished_WhenRowsRemain_ShouldCountIndexedAndReportQueue()
    {
        var recorded = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = Listen(recorded);
        Host.Services.GetRequiredService<IJobMetrics>().SearchPassFinished(3, 5);

        listener.RecordObservableInstruments();

        recorded.Single(r => r.Name == "shiplean.search.indexed").Value.ShouldBe(3);
        recorded.Single(r => r.Name == "shiplean.search.index_queue").Value.ShouldBe(5);
        recorded.Single(r => r.Name == "shiplean.realtime.presence_screens").Value.ShouldBe(0);
    }

    private static MeterListener Listen(List<(string Name, long Value, Dictionary<string, object?> Tags)> recorded)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "ShipLean")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(recorded, instrument.Name, value, tags));
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Add(recorded, instrument.Name, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(recorded, instrument.Name, (long)Math.Ceiling(value), tags));
        listener.Start();
        return listener;
    }

    private static void Add(List<(string Name, long Value, Dictionary<string, object?> Tags)> recorded, string name, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        lock (recorded)
        {
            recorded.Add((name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        }
    }
}

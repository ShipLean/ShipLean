using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Telemetry;

// Not parallel: the endpoint goes in through the process environment (see TelemetryEnabledTests).
[TestFixture]
[NonParallelizable]
public class TelemetryTracesTests : ApiFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private readonly List<Activity> _exported = [];
    private string? _previous;

    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_exported));

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
    public async Task Request_WhenItReadsPostgres_ShouldExportServerSpanWithDbChildAndNoSecrets()
    {
        var response = await Client.GetAsync("api/projects?code=oauth-code-value&state=oauth-state-value");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        SpinWait.SpinUntil(() => Snapshot().Any(a => a.Kind == ActivityKind.Server && a.OperationName.Contains("Microsoft.AspNetCore")), TimeSpan.FromSeconds(10)).ShouldBeTrue();

        var spans = Snapshot();
        var server = spans.First(a => a.Kind == ActivityKind.Server && a.GetTagItem("http.route") is not null);
        server.GetTagItem("http.route").ShouldBe("/api/projects");
        spans.Any(a => a.Source.Name == "Npgsql" && a.TraceId == server.TraceId && IsDescendant(a, server, spans)).ShouldBeTrue();

        foreach (var value in spans.SelectMany(a => a.TagObjects).Select(tag => tag.Value?.ToString() ?? string.Empty))
        {
            value.ShouldNotContain(TestUser.Joel.Email!);
            value.ShouldNotContain("oauth-code-value");
            value.ShouldNotContain("oauth-state-value");
        }
    }

    private Activity[] Snapshot()
    {
        lock (_exported)
        {
            return [.. _exported];
        }
    }

    private static bool IsDescendant(Activity child, Activity ancestor, Activity[] spans)
    {
        for (var parent = child.ParentSpanId; parent != default;)
        {
            if (parent == ancestor.SpanId)
            {
                return true;
            }

            parent = spans.FirstOrDefault(a => a.SpanId == parent)?.ParentSpanId ?? default;
        }

        return false;
    }
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Telemetry;

// Not parallel: the endpoint goes in through the process environment, which every host starting at the same time
// would also see.
[TestFixture]
[NonParallelizable]
public class TelemetryEnabledTests : ApiFixture
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private string? _previous;

    // The module reads configuration while Program.cs runs, before in-memory test settings are added, so the
    // endpoint goes in through the process environment, which is configuration from the start.
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
    public async Task Host_WhenOtlpEndpointIsDead_ShouldStartAndServeHealth()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Host.Services.GetService<TracerProvider>().ShouldNotBeNull();
        Host.Services.GetService<MeterProvider>().ShouldNotBeNull();
    }
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Telemetry;

[TestFixture]
public class TelemetryDisabledTests : ApiFixture
{
    [Test]
    public void Host_WhenNoOtlpEndpoint_ShouldRegisterNoProviders()
    {
        Host.Services.GetService<TracerProvider>().ShouldBeNull();
        Host.Services.GetService<MeterProvider>().ShouldBeNull();
    }
}

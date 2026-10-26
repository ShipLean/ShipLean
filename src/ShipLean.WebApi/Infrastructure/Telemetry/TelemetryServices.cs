using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// Exports over OTLP only when OTEL_EXPORTER_OTLP_ENDPOINT is set; otherwise nothing is registered. The pipelines start
// empty: other modules attach instrumentation with ConfigureOpenTelemetryTracerProvider / MeterProvider.
public sealed class TelemetryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[TelemetryResource.EndpointKey]))
        {
            return;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => TelemetryResource.Configure(resource, configuration))
            .WithTracing(_ => { })
            .WithMetrics(_ => { })
            .UseOtlpExporter();
    }
}

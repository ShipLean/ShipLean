using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using ShipLean.Contracts;
using ShipLean.Contracts.Telemetry;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// Attaches the framework, runtime, Npgsql and ShipLean meters to the metrics pipeline TelemetryServices starts (a no-op when no
// OTLP endpoint is set, since then no MeterProvider exists to configure). The instruments themselves always exist; they cost
// nothing while nothing listens.
public sealed class ShipLeanMetricsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ShipLeanMetrics>();
        services.AddSingleton<IJobMetrics>(sp => sp.GetRequiredService<ShipLeanMetrics>());
        services.ConfigureOpenTelemetryMeterProvider(metrics => metrics
            .AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "Microsoft.AspNetCore.Http.Connections",
                "System.Net.Http",
                "Npgsql",
                TelemetryNames.Meter)
            .AddRuntimeInstrumentation());
    }
}

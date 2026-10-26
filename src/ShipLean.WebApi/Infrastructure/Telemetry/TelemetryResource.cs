using System.Reflection;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// The resource every signal carries (traces, metrics, and Serilog logs through the same builder).
internal static class TelemetryResource
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string ServiceNameKey = "OTEL_SERVICE_NAME";
    public const string DefaultServiceName = "shiplean";

    // One id per process: the machine name says where, the GUID tells a restart apart.
    private static readonly string InstanceId = $"{System.Net.Dns.GetHostName()}-{Guid.NewGuid():N}";

    public static ResourceBuilder Configure(ResourceBuilder builder, IConfiguration configuration)
    {
        var name = configuration[ServiceNameKey];
        return builder.AddService(
            serviceName: string.IsNullOrWhiteSpace(name) ? DefaultServiceName : name,
            serviceVersion: Version(),
            serviceInstanceId: InstanceId);
    }

    public static string Version()
    {
        var assembly = typeof(TelemetryResource).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}

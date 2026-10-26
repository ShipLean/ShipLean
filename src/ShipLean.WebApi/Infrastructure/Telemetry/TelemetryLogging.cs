using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// Adds the OTLP log sink beside the console and Seq sinks, only when OTEL_EXPORTER_OTLP_ENDPOINT is set. Endpoint,
// protocol and headers come from the same standard OTEL_* settings the trace and metric exporter reads. Serilog stamps
// each record with Activity.Current's trace and span ids, which the sink sends as the log record's own ids.
public static class TelemetryLogging
{
    public const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string HeadersKey = "OTEL_EXPORTER_OTLP_HEADERS";
    private const string HttpProtobuf = "http/protobuf";
    private const string LogsPath = "v1/logs";

    public static LoggerConfiguration AddOtlpSink(this LoggerConfiguration logger, IConfiguration configuration)
    {
        var endpoint = configuration[TelemetryResource.EndpointKey];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return logger;
        }

        var http = string.Equals(configuration[ProtocolKey]?.Trim(), HttpProtobuf, StringComparison.OrdinalIgnoreCase);
        var headers = ParseHeaders(configuration[HeadersKey]);
        var attributes = TelemetryResource.Configure(ResourceBuilder.CreateEmpty(), configuration).Build().Attributes;

        return logger.WriteTo.OpenTelemetry(options =>
        {
            // Over HTTP the exporter appends the signal path to the base endpoint; the log sink wants it spelled out.
            options.Endpoint = http ? $"{endpoint.TrimEnd('/')}/{LogsPath}" : endpoint;
            options.Protocol = http ? OtlpProtocol.HttpProtobuf : OtlpProtocol.Grpc;
            foreach (var header in headers)
            {
                options.Headers[header.Key] = header.Value;
            }

            foreach (var attribute in attributes)
            {
                options.ResourceAttributes[attribute.Key] = attribute.Value;
            }
        });
    }

    // "key1=value1,key2=value2", as OTEL_EXPORTER_OTLP_HEADERS spells it.
    private static IEnumerable<KeyValuePair<string, string>> ParseHeaders(string? value)
    {
        foreach (var pair in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=', StringComparison.Ordinal);
            if (index > 0)
            {
                yield return new(pair[..index].Trim(), Uri.UnescapeDataString(pair[(index + 1)..].Trim()));
            }
        }
    }
}

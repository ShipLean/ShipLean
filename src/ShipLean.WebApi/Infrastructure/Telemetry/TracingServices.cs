using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Trace;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Infrastructure.Telemetry;

// What the tracer provider (started by TelemetryServices, only when an OTLP endpoint is set) listens to: ASP.NET Core,
// Npgsql (so Dapper), Redis when it is configured, outbound HttpClient, the MCP SDK's own source, and ShipLean's own spans.
// Nothing here records a query string, a header, a parameter value or an argument: the OAuth code and state, bearer
// tokens and the people in the data stay out of the traces.
public sealed class TracingServices : IServiceModule
{
    // The MCP SDK's own ActivitySource (ModelContextProtocol 2.3); it records the method and tool name, no arguments.
    public const string McpSdkSource = "Experimental.ModelContextProtocol";

    private static readonly string[] ServedPrefixes = ["/api", "/mcp", "/oauth", "/.well-known"];

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .SetSampler(new TraceSampling())
            .AddSource(TelemetryNames.ActivitySource, McpSdkSource)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = context => IsTraced(context.Request.Path);
                options.EnrichWithHttpResponse = (activity, response) => UseRouteTemplate(activity, response.HttpContext.GetEndpoint(), response.HttpContext.Request.Method);
                options.EnrichWithException = (activity, exception) => activity.SetStatus(ActivityStatusCode.Error);
            })
            .AddNpgsql()
            .AddHttpClientInstrumentation(options =>
            {
                options.EnrichWithHttpRequestMessage = (activity, request) => RedactUrl(activity, request.RequestUri);
                options.EnrichWithHttpResponseMessage = (activity, response) => RedactUrl(activity, response.RequestMessage?.RequestUri);
                options.EnrichWithException = (activity, exception) => activity.SetStatus(ActivityStatusCode.Error);
            })
            .AddRedisInstrumentation()
            // Only when SHIPLEAN_REDIS_CONNECTION is set is there a RedisRealtime; with none, the instrumentation has no connection to watch.
            .ConfigureRedisInstrumentation((serviceProvider, instrumentation) =>
            {
                if (serviceProvider.GetService<RedisRealtime>() is { } redis)
                {
                    _ = AddConnectionAsync(redis, instrumentation);
                }
            }));

    // The API, the MCP endpoint and the OAuth and discovery controllers; not /health (the container's probe), the
    // SignalR hub (one span for a whole websocket) or the SPA's static assets and fallback pages.
    public static bool IsTraced(PathString path)
        => !path.StartsWithSegments("/api/health")
           && ServedPrefixes.Any(prefix => path.StartsWithSegments(prefix));

    // The route template ("/api/projects/{key}"), not the raw path; no query string.
    private static void UseRouteTemplate(Activity activity, Endpoint? endpoint, string method)
    {
        activity.SetTag("url.query", null);
        activity.SetTag("url.path", null);
        if (endpoint is RouteEndpoint { RoutePattern.RawText: { } template })
        {
            var route = template.StartsWith('/') ? template : "/" + template;
            activity.SetTag("http.route", route);
            activity.DisplayName = $"{method} {route}";
        }
    }

    // Scheme, host and path only: no credentials in the authority, no query string.
    private static void RedactUrl(Activity activity, Uri? uri)
    {
        if (uri is { IsAbsoluteUri: true })
        {
            activity.SetTag("url.full", uri.GetLeftPart(UriPartial.Path).Replace(uri.UserInfo + "@", string.Empty, StringComparison.Ordinal));
        }
    }

    // The connection opens lazily and on its own schedule, so the profiler is attached once it exists.
    private static async Task AddConnectionAsync(RedisRealtime redis, OpenTelemetry.Instrumentation.StackExchangeRedis.StackExchangeRedisInstrumentation instrumentation)
    {
        try
        {
            instrumentation.AddConnection((await redis.DatabaseAsync()).Multiplexer);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // No Redis, no Redis spans; the realtime code reports the failure itself.
        }
    }
}

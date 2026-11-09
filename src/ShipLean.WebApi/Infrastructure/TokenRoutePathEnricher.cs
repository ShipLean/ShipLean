using Serilog.Core;
using Serilog.Events;

namespace ShipLean.WebApi.Infrastructure;

// Keeps URL tokens out of the logs. The hosting log scope carries the raw RequestPath, and Serilog copies scope
// properties onto every event logged during the request (console, Seq, OTLP). On a route whose pattern has a
// {token} parameter (api/uploads/{token}, api/uploads/payloads/{token}, api/downloads/{token}) that path is the
// credential, so this swaps it for the route pattern. Every other request keeps its RequestPath.
// Added after Enrich.FromLogContext() in Program.cs: the scope property is already on the event (scopes are added
// only if absent, so the provider's own pass afterwards leaves this value alone). currentEndpoint reads the request's
// matched endpoint through IHttpContextAccessor (a Func, like RequestOrigin: AG0009 refuses the accessor itself).
public sealed class TokenRoutePathEnricher(Func<Endpoint?> currentEndpoint) : ILogEventEnricher
{
    public const string PropertyName = "RequestPath";
    public const string TokenParameter = "token";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (currentEndpoint() is not RouteEndpoint { RoutePattern: var pattern }
            || pattern.GetParameter(TokenParameter) is null)
        {
            return;
        }

        logEvent.AddOrUpdateProperty(new LogEventProperty(PropertyName, new ScalarValue(pattern.RawText ?? "(token route)")));
    }
}

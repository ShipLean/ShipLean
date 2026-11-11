using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Logging;

// Runs the enricher in the same Serilog pipeline Program.cs builds (FromLogContext, then the enricher, through
// SerilogLoggerFactory), with the request path in a logger scope the way ASP.NET Core's hosting scope carries it.
// So these also pin that the enricher's value wins over the scope's.
public sealed class TokenRoutePathEnricherTests
{
    private const string TokenPath = "/api/uploads/payloads/upload-token-4c7e19ab2f";

    [Test]
    public void Enrich_WhenRouteHasTokenParameter_ShouldLogTheRouteTemplate()
    {
        var logged = LoggedRequestPath(TokenPath, "api/uploads/payloads/{token}");

        logged.ShouldBe("api/uploads/payloads/{token}");
    }

    [Test]
    public void Enrich_WhenRouteHasNoTokenParameter_ShouldKeepTheRequestPath()
    {
        const string path = "/api/projects/0b0e6c55-5a1e-4d43-9d3c-1f2a3b4c5d6e";

        var logged = LoggedRequestPath(path, "api/projects/{id:guid}");

        logged.ShouldBe(path);
    }

    [Test]
    public void Enrich_WhenNoEndpointMatched_ShouldKeepTheRequestPath()
    {
        var logged = LoggedRequestPath(TokenPath, routeTemplate: null);

        logged.ShouldBe(TokenPath);
    }

    private static object? LoggedRequestPath(string requestPath, string? routeTemplate)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        if (routeTemplate is not null)
        {
            accessor.HttpContext.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(routeTemplate), 0, EndpointMetadataCollection.Empty, routeTemplate));
        }

        var recorded = new RecordedLogEvents();
        using var serilog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With(new TokenRoutePathEnricher(() => accessor.HttpContext?.GetEndpoint()))
            .WriteTo.Sink(recorded)
            .CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var logger = factory.CreateLogger("Test");

        using (logger.BeginScope(new List<KeyValuePair<string, object>> { new("RequestId", "0HN1:00000001"), new("RequestPath", requestPath) }))
        {
            logger.LogInformation("Inside the request");
        }

        return (recorded.Events.Single().Properties[TokenRoutePathEnricher.PropertyName] as ScalarValue)?.Value;
    }
}

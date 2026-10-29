using System.Reflection;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Infrastructure;

// /health is what the container HEALTHCHECK calls; /api/health is the same payload under /api.
// The Docker build stamps the commit into the informational version (-p:SourceRevisionId=...,
// "1.0.0+<sha>"), so the response names the running build. Both answer without a session.
public sealed class HealthEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var version = ApiMarker.Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        IResult Health(IEmbedder embedder, PublicOrigin origins)
            => Results.Ok(new HealthReport("ok", version, SearchOf(embedder), origins.HealthValue));
        endpoints.MapGet("/health", Health).AllowAnonymous();
        endpoints.MapGet("/api/health", Health).AllowAnonymous();
    }

    private static HealthSearch SearchOf(IEmbedder embedder)
        => new(embedder.Status.Mode, embedder.Status.Reason, embedder.ModelId?.ToString());
}

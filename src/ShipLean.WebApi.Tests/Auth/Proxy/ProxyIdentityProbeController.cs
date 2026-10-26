using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Proxy;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A route that exists only in the reader tests' hosts (added as an application part): anonymous, it answers what the
// host's IProxyIdentityReader returns for this very request, so the reader is tested through the real pipeline
// (forwarded headers, raw-peer capture) without the sign-in handler. Production has no such endpoint.
[AllowAnonymous]
[Route("api/test-proxy")]
public sealed class ProxyIdentityProbeController(IProxyIdentityReader reader) : ApiControllerBase
{
    public const string Path = "/api/test-proxy/identity";

    [HttpGet("identity")]
    [ProducesResponseType<Probe>(StatusCodes.Status200OK)]
    public async Task<Probe> Identity(CancellationToken ct) => new(await reader.ReadAsync(Request, ct));

    // For ConfigureProxyTestServices.
    public static void AddTo(IServiceCollection services) => services.AddControllers().AddApplicationPart(typeof(ProxyIdentityProbeController).Assembly);

    // What the reader returned for this client's next request.
    public static async Task<ProxyIdentity?> ReadAsync(ProxyApiClient client) => (await client.GetJsonAsync<Probe>(Path)).Identity;

    public sealed record Probe(ProxyIdentity? Identity);
}

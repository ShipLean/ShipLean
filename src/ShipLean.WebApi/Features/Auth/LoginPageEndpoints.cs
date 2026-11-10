using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Auth;

// /login and /signup serve the SPA to anyone, so those pages load before there is a session. Static-file fallbacks,
// not API routes, so they stay an endpoint module (a controller action would change how index.html is served).
public sealed class LoginPageEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFallbackToFile("/login", "index.html").AllowAnonymous();
        endpoints.MapFallbackToFile("/signup", "index.html").AllowAnonymous();
    }
}

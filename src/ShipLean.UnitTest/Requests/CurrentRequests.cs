using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.UnitTest.Requests;

// The real CurrentRequest for the classes that read the request through it. HttpContextAccessor keeps the context in an
// AsyncLocal, so a test sets Accessor.HttpContext for its own flow only.
public static class CurrentRequests
{
    public static HttpContextAccessor Accessor { get; } = new();

    public static CurrentRequest Create(ForwardedHeadersOptions? forwarding = null)
        => new(new AccessorOnly(), Options.Create(forwarding ?? new ForwardedHeadersOptions()));

    // A CurrentRequest whose request is context (or none).
    public static CurrentRequest Over(HttpContext? context)
    {
        Accessor.HttpContext = context;
        return Create();
    }

    private sealed class AccessorOnly : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IHttpContextAccessor) ? Accessor : null;
    }
}

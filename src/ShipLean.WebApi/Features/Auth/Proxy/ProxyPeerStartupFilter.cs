using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Records the raw TCP peer (ProxyPeer) on every request, ahead of the whole app pipeline and so ahead of
// UseForwardedHeaders. ProxyIdentityServices registers it as the first startup filter, which makes it the outermost.
public sealed class ProxyPeerStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Features.Set(new ProxyPeer(context.Connection.RemoteIpAddress));
            return nextMiddleware(context);
        });
        next(app);
    };
}

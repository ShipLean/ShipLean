using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Http;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Links.PublicFetch;

// The two named clients behind PublicFetcher. Both resolve a host once and connect to the vetted address (PinnedConnector), never
// follow redirects themselves (PublicFetcher does, so each hop picks its client), send no cookies and use no proxy (a proxy would
// connect for us and skip the check). The resolver and address check are shared with the agent metadata client.
public sealed class PublicFetchServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(PublicFetchOptions.Default);
        services.TryAddSingleton<IHostResolver, DnsHostResolver>();
        services.TryAddSingleton<IAddressCheck, PublicAddressCheck>();
        services.TryAddSingleton<PinnedConnector>();
        services.AddHttpClient(PublicFetchOptions.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => Handler(sp.GetRequiredService<PinnedConnector>()));
        services.AddHttpClient(PublicFetchOptions.AllowedHostClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => Handler(new PinnedConnector(sp.GetRequiredService<IHostResolver>(), new AnyAddressCheck())));
    }

    private static SocketsHttpHandler Handler(PinnedConnector connector) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectCallback = connector.ConnectAsync,
    };
}

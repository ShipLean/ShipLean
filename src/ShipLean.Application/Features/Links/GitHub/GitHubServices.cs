using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitHub;

// The GitHub source over its named HttpClient. The client connects through the pinned, address-checked connector and follows
// no redirects, so the token only ever goes to the configured API address.
public sealed class GitHubServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IHostResolver, DnsHostResolver>();
        services.TryAddSingleton<IAddressCheck, PublicAddressCheck>();
        services.TryAddSingleton<PinnedConnector>();
        services.AddHttpClient(GitHubSource.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ConnectCallback = sp.GetRequiredService<PinnedConnector>().ConnectAsync,
            });
        services.AddScoped<ILinkCardSource, GitHubSource>();
    }
}

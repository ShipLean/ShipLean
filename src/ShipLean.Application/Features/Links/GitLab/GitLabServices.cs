using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitLab;

// The GitLab source over its named HttpClient. The client connects through the pinned connector and follows no redirects, so
// the token only ever goes to the configured GitLab address. It allows any address: that address is the GitLab URL an instance
// admin set, and a self-hosted GitLab is often on a private network.
public sealed class GitLabServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IHostResolver, DnsHostResolver>();
        services.AddHttpClient(GitLabSource.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ConnectCallback = new PinnedConnector(sp.GetRequiredService<IHostResolver>(), new AnyAddressCheck()).ConnectAsync,
            });
        services.AddScoped<ILinkCardSource, GitLabSource>();
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// The code host readers have their own named clients, set up like the link sources' (pinned, address-checked connector, no
// redirects), so the token only ever goes to the configured address. The patch cache they sit behind is registered by
// CodePatchCacheServices in the WebApi, which knows whether Redis is configured.
public sealed class CodeHostServices : IServiceModule
{
    public const string GitHubClientName = "code-host-github";
    public const string GitLabClientName = "code-host-gitlab";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IHostResolver, DnsHostResolver>();
        services.TryAddSingleton<IAddressCheck, PublicAddressCheck>();
        services.TryAddSingleton<PinnedConnector>();
        AddPinnedClient(services, GitHubClientName);
        AddPinnedClient(services, GitLabClientName);
        services.AddScoped<GitHubCodeHost>();
        services.AddScoped<GitLabCodeHost>();
        services.AddScoped<CodeHostRouter>();
        services.AddScoped<ICodeHost>(sp => new CachingCodeHost(sp.GetRequiredService<CodeHostRouter>(), sp.GetRequiredService<ICodePatchCache>()));
    }

    private static void AddPinnedClient(IServiceCollection services, string name)
    {
        services.AddHttpClient(name, client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                ConnectCallback = sp.GetRequiredService<PinnedConnector>().ConnectAsync,
            });
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// The code host readers use the named clients of the link sources (pinned connector, no redirects). The patch cache they sit
// behind is registered by CodePatchCacheServices in the WebApi, which knows whether Redis is configured.
public sealed class CodeHostServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GitHubCodeHost>();
        services.AddScoped<GitLabCodeHost>();
        services.AddScoped<CodeHostRouter>();
        services.AddScoped<ICodeHost>(sp => new CachingCodeHost(sp.GetRequiredService<CodeHostRouter>(), sp.GetRequiredService<ICodePatchCache>()));
    }
}

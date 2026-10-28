using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Webhooks;

namespace ShipLean.Application.Features.Webhooks;

public sealed class WebhooksServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPullRequestLinks, PullRequestLinks>();
    }
}

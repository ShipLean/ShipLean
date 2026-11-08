using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.IssueIntake.Outbox;

// Delivery of github_outbox (D15): the deliverer and its hosted loop. The test and local host profiles remove the loop, so
// nothing posts to GitHub from a test or a developer's machine.
public sealed class OutboxServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<GitHubOutboxDeliverer>();
        services.AddHostedService<GitHubOutboxDeliveryService>();
    }
}

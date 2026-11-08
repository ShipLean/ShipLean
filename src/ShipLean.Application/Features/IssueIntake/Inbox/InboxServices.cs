using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake.ShippedIn;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.IssueIntake.Inbox;

public sealed class InboxServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProposalJobWorker>();
        // The verified "shipped in" facts; SHIP-763 (progress) reuses them for its "fixed in" message.
        services.AddSingleton<ShippedInFacts>();
        // HostProfiles removes it in the Test and Local profiles, where tests drive ProposalJobWorker.RunNextAsync directly.
        services.AddHostedService<ProposalJobService>();
    }
}

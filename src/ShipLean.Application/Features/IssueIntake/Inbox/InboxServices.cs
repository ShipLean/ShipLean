using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.IssueIntake.Inbox;

public sealed class InboxServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProposalJobWorker>();
        // HostProfiles removes it in the Test and Local profiles, where tests drive ProposalJobWorker.RunNextAsync directly.
        services.AddHostedService<ProposalJobService>();
    }
}

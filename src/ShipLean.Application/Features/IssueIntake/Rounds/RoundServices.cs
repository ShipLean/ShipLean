using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

public sealed class RoundServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<RoundHolds>();
        services.AddSingleton<IIssueProposalSessions, IssueProposalSessions>();
        services.AddSingleton<ClosestItems>();
        services.AddSingleton<RoundJobWorker>();
        // TODO: register RoundJobService with AddHostedService once HostProfiles removes it from the Test profile (a file this lane
        // doesn't own); until then round jobs wait in issue_job and tests drive RoundJobWorker.RunNextAsync directly.
    }
}

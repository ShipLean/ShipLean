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
        // HostProfiles removes it in the Test and Local profiles, where tests drive RoundJobWorker.RunNextAsync directly.
        services.AddHostedService<RoundJobService>();
    }
}

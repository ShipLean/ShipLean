using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

public sealed class RoundServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // TODO: the real proposal sessions and the round worker replace this registration.
        services.AddSingleton<IIssueProposalSessions, NotBuiltProposalSessions>();
    }
}

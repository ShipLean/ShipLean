using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

public sealed class AcceptanceServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIssueAcceptance, DbIssueAcceptance>();
        services.AddSingleton<AcceptanceReads>();
        services.AddSingleton<AcceptJobWorker>();
        // TODO: register AcceptJobService with AddHostedService once HostProfiles removes it from the Test profile (a file this lane
        // doesn't own); until then accept jobs wait in issue_job and tests drive AcceptJobWorker.RunNextAsync directly.
    }
}

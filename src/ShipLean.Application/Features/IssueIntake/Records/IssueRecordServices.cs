using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

public sealed class IssueRecordServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IGitHubIssueStore, DbGitHubIssueStore>();
        services.AddSingleton<IIssueEffects, DbIssueEffects>();
        services.AddSingleton<IIssueJobs, DbIssueJobs>();
        services.AddScoped<IMirroredComments, DbMirroredComments>();
    }
}

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
        // TODO: the real GitHubIdentity (GET /user with the instance token, cached) replaces this registration and UnbuiltGitHubIdentity.
        services.AddSingleton<IGitHubIdentity, UnbuiltGitHubIdentity>();
    }
}

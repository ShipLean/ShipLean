using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.GitHub;

// GitHub writes (C3). The client uses the github-source HttpClient that GitHubServices sets up (pinned connector, no redirects).
// It is scoped because the integration settings are; GitHubIdentity is a singleton so its cache lasts.
public sealed class GitHubWriteServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GitHubIssuesClient>();
        services.AddScoped<IGitHubIssues>(sp => sp.GetRequiredService<GitHubIssuesClient>());
        services.AddSingleton<IGitHubIdentity, GitHubIdentity>();
        services.AddSingleton<IGitHubOutbox, DbGitHubOutbox>();
        // TODO: the real image publications replace this registration and UnknownImagePublications.
        services.AddSingleton<IImagePublications, UnknownImagePublications>();
    }
}

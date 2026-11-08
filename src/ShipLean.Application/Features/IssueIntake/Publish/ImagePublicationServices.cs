using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Publish;

// Image publications (D14). A singleton so its cache lasts; it resolves the scoped GitHubIssuesClient per lookup.
public sealed class ImagePublicationServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IImagePublications, GitHubImagePublications>();
}

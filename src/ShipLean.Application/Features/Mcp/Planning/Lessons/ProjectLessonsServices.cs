using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Mcp.Planning.Lessons;

// Scoped, not singleton: it opens attachments through IAttachmentStore, which is scoped.
public sealed class ProjectLessonsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProjectLessons>();
        services.AddSingleton<FeatureKind>();
    }
}

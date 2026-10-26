using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;

namespace ShipLean.Application.Features.Backlog.Stories;

public sealed class StoriesBacklogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<BacklogPlanning>();
        services.AddScoped<IStoriesBacklogReads, StoriesBacklogReads>();
    }
}

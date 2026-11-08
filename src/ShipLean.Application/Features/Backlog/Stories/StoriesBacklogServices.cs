using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;

namespace ShipLean.Application.Features.Backlog.Stories;

public sealed class StoriesBacklogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<BacklogTriage>();
        services.AddScoped<BacklogPlanning>();
        services.AddScoped<IStoriesBacklogReads, StoriesBacklogReads>();
    }
}

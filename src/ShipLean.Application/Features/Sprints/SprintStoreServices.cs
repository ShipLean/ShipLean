using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

// Scoped: the store consumes IWorkItemStore for rollover, and the real work item store is per request.
public sealed class SprintStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISprintStore, SprintStore>();
        services.AddScoped<ISprintCapacityRecompute, SprintCapacityRecompute>();
    }
}

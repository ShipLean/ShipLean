using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems.Reads.Changes;

namespace ShipLean.Application.Features.WorkItems.Reads.Changes;

public sealed class ChangesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IWorkItemChanges, WorkItemChanges>();
}

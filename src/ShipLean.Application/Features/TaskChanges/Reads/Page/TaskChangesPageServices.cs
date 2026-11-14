using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Reads.Page;

public sealed class TaskChangesPageServices : IServiceModule
{
    // TODO: register the real page read in place of the stub.
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ITaskChangesPage, StubTaskChangesPage>();
}

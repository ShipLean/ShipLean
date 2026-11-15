using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed class WorkItemStoreServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkItemStore, WorkItemStore>();
        services.AddScoped<IWorkItemTreeWriter, WorkItemStore>();
        services.AddScoped<IWorkItemBatchWriter, WorkItemStore>();
        services.AddScoped<IWorkItemBlockWriter, WorkItemStore>();

        // IRevisionNotes lives here because the work item store is the only writer of revision rows.
        services.AddScoped<IRevisionNotes, WorkItemStore>();
    }
}

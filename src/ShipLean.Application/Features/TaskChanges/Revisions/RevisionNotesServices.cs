using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Revisions;

// IRevisionNotes is written by the work item store, the only writer of revision rows.
public sealed class RevisionNotesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<IRevisionNotes, WorkItemStore>();
}

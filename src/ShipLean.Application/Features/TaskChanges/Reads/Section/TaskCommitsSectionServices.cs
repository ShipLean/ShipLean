using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Reads.Section;

public sealed class TaskCommitsSectionServices : IServiceModule
{
    // Registers the section read.
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ITaskCommitsSection, TaskCommitsSection>();
}

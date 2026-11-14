using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Reads.Section;

public sealed class TaskCommitsSectionServices : IServiceModule
{
    // TODO: register the real section read in place of the stub.
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ITaskCommitsSection, StubTaskCommitsSection>();
}

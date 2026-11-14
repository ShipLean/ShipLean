using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Commits;

public sealed class TaskCommitsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ITaskCommits, StubTaskCommits>();
}

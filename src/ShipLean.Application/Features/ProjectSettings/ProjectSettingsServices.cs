using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.ProjectSettings;

public sealed class ProjectSettingsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddSingleton<IProjectAgentSettings, DbProjectAgentSettings>();
}

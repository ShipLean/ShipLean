using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

public sealed class CodeHostServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ICodeHost, StubCodeHost>();
}

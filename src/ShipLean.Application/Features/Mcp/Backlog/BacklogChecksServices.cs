using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;

namespace ShipLean.Application.Features.Mcp.Backlog;

public sealed class BacklogChecksServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IBacklogChecks, BacklogChecks>();
}

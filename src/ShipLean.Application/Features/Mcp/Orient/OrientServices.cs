using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Mcp.Orient;

// A singleton like the MCP resolvers: the user is read from ICurrentUser at call time.
public sealed class OrientServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<OrientReads>();
}

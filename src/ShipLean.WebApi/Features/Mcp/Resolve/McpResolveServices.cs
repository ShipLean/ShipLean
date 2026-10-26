using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// The resolvers every MCP tool calls instead of writing its own lookup. Singletons, like the other readers: the user is
// read from ICurrentUser at call time.
public sealed class McpResolveServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ProjectResolver>();
        services.AddSingleton<ItemResolver>();
        services.AddSingleton<SprintResolver>();
        services.AddSingleton<StateResolver>();
        services.AddSingleton<MemberResolver>();
    }
}

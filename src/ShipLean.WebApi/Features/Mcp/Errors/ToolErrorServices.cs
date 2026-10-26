using ShipLean.Contracts;
using ShipLean.WebApi.Features.Mcp.Scopes;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// One filter, in one order: errors outermost, so a scope refusal or a tool's exception both come back as an McpError.
public sealed class ToolErrorServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer().WithRequestFilters(filters => filters.AddCallToolFilter(next => ToolTraceFilter.Apply(ToolErrorFilter.Apply(ToolScopeFilter.Apply(next)))));
}

using ShipLean.Contracts;
using ShipLean.WebApi.Features.Mcp.Payloads;
using ShipLean.WebApi.Features.Mcp.Scopes;
using ShipLean.WebApi.Features.Mcp.Usage;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// One filter, in one order: errors outside the payload and scope filters, so a payload refusal, a scope refusal or a tool's
// exception all come back as an McpError; the payload filter outside the scope backstop, so it sees the expanded arguments.
public sealed class ToolErrorServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer().WithRequestFilters(filters => filters.AddCallToolFilter(next =>
            ToolMetricsFilter.Apply(ToolUsageFilter.Apply(ToolTraceFilter.Apply(ToolErrorFilter.Apply(PayloadFilter.Apply(ToolScopeFilter.Apply(next))))))));
}

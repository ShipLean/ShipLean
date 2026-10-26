using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// The prompt counterpart of ToolErrorServices: an ApiException a prompt throws would otherwise reach the client as a bare "An error occurred."
public sealed class PromptErrorServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer().WithRequestFilters(filters => filters.AddGetPromptFilter(PromptErrorFilter.Apply));
}

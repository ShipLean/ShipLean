using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// What the server tells an agent when it connects. Program.cs adds the MCP server and its tools; this adds to it.
public sealed class McpServerServices : IServiceModule
{
    public const string Instructions = PromptDefaults.ServerInstructions;
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddMcpServer(options => options.ServerInstructions = Instructions);
}

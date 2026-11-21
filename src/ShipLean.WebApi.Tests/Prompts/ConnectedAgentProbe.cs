using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Prompts;

// Test-only: /mcp-probe/agent under the agent policy returns what IConnectedAgent says for the request.
public sealed class ConnectedAgentProbe : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Map("/mcp-probe/agent", branch =>
        {
            branch.UseRouting();
            branch.UseAuthorization();
            branch.UseEndpoints(endpoints => endpoints.MapGet("/", (IConnectedAgent agent) => Results.Json(agent.Current))
                .RequireAuthorization(AgentPolicy.Name));
        });
        next(app);
    };
}

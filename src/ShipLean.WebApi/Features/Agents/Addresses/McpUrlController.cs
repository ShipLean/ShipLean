using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Addresses;

// The address a person pastes into an agent. Any signed-in user may read it.
[Route("api/agents/mcp-url")]
public sealed class McpUrlController(IAgentAddresses addresses) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<McpUrlDto>(StatusCodes.Status200OK)]
    public async Task<McpUrlDto> Get(CancellationToken cancellationToken)
        => new((await addresses.ForCurrentRequestAsync(cancellationToken)).Resource);
}

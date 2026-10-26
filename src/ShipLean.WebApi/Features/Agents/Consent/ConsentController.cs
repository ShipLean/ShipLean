using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Consent;

// The consent page's two calls, for the signed-in user. The cookie only: a bearer is never accepted on /api. A request
// that is unknown, expired or another user's is not_found.
[Route("api/agents/consent")]
public sealed class ConsentController(IAgentConsent consent) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ConsentRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ConsentRequestDto> Get(Guid id, CancellationToken cancellationToken) => consent.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}")]
    [ProducesResponseType<ConsentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ConsentResult> Decide(Guid id, [FromBody] ConsentDecision decision, CancellationToken cancellationToken)
        => consent.DecideAsync(id, decision, cancellationToken);
}

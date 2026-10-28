using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.AgentDispatch;

[Route("api/work-items/{id:guid}/agent-session")]
public sealed class AgentSessionController(AgentSessionEndpoints endpoints, IOptions<ForwardedHeadersOptions> forwarding) : ApiControllerBase
{
    // Start agent: opens a session by hand when the item has no live one (the lost-event and retry cases).
    [HttpPost]
    [ProducesResponseType<AgentSessionStartedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AgentSessionStartedDto> Start(Guid id, CancellationToken cancellationToken)
        => endpoints.StartAsync(id, RequestOrigin.Of(Request, forwarding.Value), cancellationToken);

    // Whether Start agent would work now, and why not if it would not.
    [HttpGet("availability")]
    [ProducesResponseType<AgentStartAvailabilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentStartAvailabilityDto> Availability(Guid id, CancellationToken cancellationToken)
        => endpoints.AvailabilityAsync(id, cancellationToken);
}

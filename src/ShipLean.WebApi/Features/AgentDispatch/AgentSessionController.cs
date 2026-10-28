using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.AgentDispatch;

[Route("api/work-items/{id:guid}")]
public sealed class AgentSessionController(AgentSessionEndpoints endpoints, IOptions<ForwardedHeadersOptions> forwarding) : ApiControllerBase
{
    // Start agent: opens a session by hand when the item has no live one (the lost-event and retry cases).
    [HttpPost("agent-session")]
    [ProducesResponseType<AgentSessionStartedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AgentSessionStartedDto> Start(Guid id, CancellationToken cancellationToken)
        => endpoints.StartAsync(id, RequestOrigin.Of(Request, forwarding.Value), cancellationToken);

    // The Agent panel's one request: sessions newest first with their activities, and whether Start agent works.
    [HttpGet("agent-sessions")]
    [ProducesResponseType<AgentPanelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentPanelDto> Sessions(Guid id, CancellationToken cancellationToken)
        => endpoints.SessionsAsync(id, cancellationToken);

    // Whether Start agent would work now, and why not if it would not.
    [HttpGet("agent-session/availability")]
    [ProducesResponseType<AgentStartAvailabilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentStartAvailabilityDto> Availability(Guid id, CancellationToken cancellationToken)
        => endpoints.AvailabilityAsync(id, cancellationToken);

    // Stop: the agent is told to stop and the session stays live until it answers.
    [HttpPost("agent-session/stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Stop(Guid id, CancellationToken cancellationToken)
        => await NoContentAfter(endpoints.StopAsync(id, RequestOrigin.Of(Request, forwarding.Value), cancellationToken));
}

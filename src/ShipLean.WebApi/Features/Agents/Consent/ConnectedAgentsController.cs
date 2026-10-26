using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Consent;

// "Connected agents" on Your profile: the caller's own authorizations. Another user's id is not_found, like an unknown one.
[Route("api/me/agents")]
public sealed class ConnectedAgentsController(IAgentAuthorizations authorizations, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConnectedAgentDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ConnectedAgentDto>> List(CancellationToken cancellationToken)
        => authorizations.ListAsync(currentUser.Id, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Revoke(Guid id, CancellationToken cancellationToken)
        => NoContentAfter(authorizations.RevokeAsync(currentUser.Id, id, cancellationToken));
}

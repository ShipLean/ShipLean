using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inbox;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inbox;

[Route("api/projects/{projectId:guid}/inbox")]
public sealed class InboxController(InboxEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InboxDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<InboxDto> Get(Guid projectId, CancellationToken cancellationToken) => endpoints.GetAsync(projectId, cancellationToken);

    [HttpGet("answered")]
    [ProducesResponseType<AnsweredDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AnsweredDto> Answered(Guid projectId, CancellationToken cancellationToken)
        => endpoints.AnsweredAsync(projectId, cancellationToken);
}

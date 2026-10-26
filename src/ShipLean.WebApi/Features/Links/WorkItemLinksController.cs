using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Links.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Links;

[Route("api/work-items/{id:guid}/work-item-links")]
public sealed class WorkItemLinksController(LinkEndpoints endpoints) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<WorkItemLinkRowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ObjectResult> Add(Guid id, AddWorkItemLinkRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await endpoints.AddWorkItemLinkAsync(id, request, cancellationToken));
}

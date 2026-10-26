using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Links.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Links;

[Route("api/work-items/{id:guid}/link-cards/refresh")]
public sealed class LinkCardsRefreshController(LinkEndpoints endpoints) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<LinkCardsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<LinkCardsDto> Refresh(Guid id, CancellationToken cancellationToken)
        => endpoints.RefreshAsync(id, cancellationToken);
}

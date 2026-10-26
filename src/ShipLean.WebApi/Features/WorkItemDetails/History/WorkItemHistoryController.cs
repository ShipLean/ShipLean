using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.History;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.History;

[Route("api/work-items/{id:guid}/history")]
public sealed class WorkItemHistoryController(HistoryEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<HistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<HistoryDto> Get(Guid id, CancellationToken cancellationToken)
        => endpoints.GetAsync(id, cancellationToken);
}

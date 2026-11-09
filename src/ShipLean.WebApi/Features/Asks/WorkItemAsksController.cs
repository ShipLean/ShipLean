using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Asks.Reads;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Asks;

[Route("api/work-items/{id:guid}/asks")]
public sealed class WorkItemAsksController(AskEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemAsksDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ItemAsksDto> Get(Guid id, CancellationToken cancellationToken)
        => endpoints.GetAsync(id, cancellationToken);
}

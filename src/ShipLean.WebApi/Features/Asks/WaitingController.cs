using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Asks.Waiting;
using ShipLean.Contracts.Asks;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Asks;

[Route("api/me/waiting")]
public sealed class WaitingController(WaitingEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<WaitingDto>(StatusCodes.Status200OK)]
    public Task<WaitingDto> Get(CancellationToken cancellationToken) => endpoints.GetAsync(cancellationToken);
}

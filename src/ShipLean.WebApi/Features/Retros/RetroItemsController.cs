using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/projects/{id:guid}/retros/items")]
public sealed class RetroItemsController(IRetroItemReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet("{key}")]
    [ProducesResponseType<RetroItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroItemDto> Get(Guid id, string key, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reader.GetAsync(id, key, cancellationToken);
    }
}

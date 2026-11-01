using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/projects/{id:guid}/retros")]
public sealed class RetroSearchController(IRetroSearchReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<RetroSearchDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroSearchDto> Search(Guid id, [FromQuery] RetroSearchQuery query, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reader.SearchAsync(id, query, cancellationToken);
    }
}

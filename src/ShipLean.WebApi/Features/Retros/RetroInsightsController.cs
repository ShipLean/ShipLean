using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/projects/{id:guid}/retros/insights")]
public sealed class RetroInsightsController(IRetroInsightsReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<RetroInsightsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroInsightsDto> Get(Guid id, [FromQuery] string? range, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reader.GetAsync(id, RetroChecks.Range(range), cancellationToken);
    }
}

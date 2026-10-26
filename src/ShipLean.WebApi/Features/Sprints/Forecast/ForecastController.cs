using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Forecast;

[Route("api/projects/{id:guid}/forecast")]
public sealed class ForecastController(VelocityOverrides overrides, IProjectAccess access) : ApiControllerBase
{
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> PutOverride(Guid id, VelocityOverridePut put, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await NoContentAfter(overrides.PutAsync(id, put, cancellationToken));
    }
}

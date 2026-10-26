using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Metrics;

[Route("api/sprints/{id:guid}/metrics")]
public sealed class SprintMetricsController(ISprintMetrics metrics, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SprintMetricsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintMetricsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await metrics.GetSprintAsync(id, cancellationToken);
    }
}

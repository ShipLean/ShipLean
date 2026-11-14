using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Usage;

// What the SPA reports about how a project's screens are used, where no write already tells the server.
[Route("api/projects/{id:guid}/usage")]
public sealed class ProjectUsageController(BacklogSorts sorts, TimelineUsage timeline) : ApiControllerBase
{
    [HttpPost("backlog-sort")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> BacklogSort(Guid id, BacklogSortDto request, CancellationToken cancellationToken)
    {
        await sorts.ReportAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("timeline-opened")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> TimelineOpened(Guid id, TimelineOpenedDto request, CancellationToken cancellationToken)
    {
        await timeline.OpenedAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("timeline-bar-selected")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> TimelineBarSelected(Guid id, CancellationToken cancellationToken)
    {
        await timeline.BarSelectedAsync(id, cancellationToken);
        return NoContent();
    }
}

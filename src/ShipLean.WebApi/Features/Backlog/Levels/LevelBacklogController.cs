using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog.Levels;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Backlog.Levels;

// GET api/projects/{id}/backlogs/{level}: the Features or Epics backlog (level = features | epics; anything else: 400 validation).
// Query: filter = a URL-encoded CriteriaJson document applied to the level's own items, evaluated on the server (invalid: 400
// validation); includeCompleted = false (default) leaves Completed-category Features or Epics out.
// Rows are in stack order, always List mode (Level says which). Rank is the place in the whole level (honouring only includeCompleted),
// so a filter never renumbers it. Rollups are the per-type counts of everything under the row at any depth, closed included
// whatever includeCompleted says, Tasks left out: Story and Bug entries drive Progress, Feature entries (nested ones too) are an
// Epic's Features count. ChildPoints is the points of all stories and bugs under the row, open and closed (null when none is
// estimated); SumWarn is own Points > 0 and ChildPoints > 1.25 x Points. TagColours are the matching level board's.
[Route("api/projects/{id:guid}/backlogs/{level}")]
public sealed class LevelBacklogController(ILevelBacklogReads reads, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BacklogViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<BacklogViewDto> Get(Guid id, BoardLevel level, string? filter, bool includeCompleted, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.GetAsync(id, level, filter, includeCompleted, cancellationToken);
    }
}

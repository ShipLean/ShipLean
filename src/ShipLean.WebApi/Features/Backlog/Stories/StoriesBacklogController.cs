using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Backlog.Stories;

// GET api/projects/{id}/backlog: the Stories backlog (stories and bugs; Features and Epics have their own endpoint).
// Query: mode = list (default) | tree | planning; filter = a URL-encoded CriteriaJson document, evaluated on the server (invalid: 400
// validation); includeCompleted = false (default) leaves Completed-category items out.
// Rank: in list and tree mode a row's Rank is its place in the whole backlog (honouring only includeCompleted), so a filter never
// renumbers it; planning sections restart rank at 1 in each section. Planning Points: a sprint's is what is committed to it (every
// live story and bug in it, completed included, ignoring the filter and includeCompleted); the Backlog section sums its visible rows.
// Group rows (Epic, Feature, No parent) count the filtered stories, bugs and nested Features beneath them, closed ones included
// whatever includeCompleted says, with no Task entries. A story under a Removed Feature is shown under the nearest live ancestor
// (its Epic), but its Parent is still the Removed Feature, so a client must not read Parent == null as "No parent".
[Route("api/projects/{id:guid}/backlog")]
public sealed class StoriesBacklogController(IStoriesBacklogReads reads, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BacklogViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<BacklogViewDto> Get(Guid id, BacklogMode mode, string? filter, bool includeCompleted, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.GetAsync(id, mode, filter, includeCompleted, cancellationToken);
    }
}

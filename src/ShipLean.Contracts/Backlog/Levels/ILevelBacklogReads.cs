using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Views;

namespace ShipLean.Contracts.Backlog.Levels;

// The Features or Epics backlog as one list view model, built over IWorkTreeReads. Callers check access first.
// level is Features or Epics (anything else throws ApiException.Validation). filter is a CriteriaJson document as JSON text
// (null or blank = none) applied to the level's own items; includeCompleted false leaves Completed-category Features or Epics out.
// Throws ApiException.Validation (the filter is not JSON, or its criteria are invalid).
public interface ILevelBacklogReads
{
    Task<BacklogViewDto> GetAsync(Guid projectId, BoardLevel level, string? filter, bool includeCompleted, CancellationToken cancellationToken = default);
}

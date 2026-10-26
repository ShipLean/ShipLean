using ShipLean.Contracts.Views;

namespace ShipLean.Contracts.Backlog;

// The Stories backlog (stories and bugs) as one view model per mode, built over IWorkTreeReads. Callers check access first.
// filter is a CriteriaJson document as JSON text (null or blank = none), evaluated here: the client never filters.
// includeCompleted false leaves Completed-category stories and bugs out. Throws ApiException.Validation (the filter is not
// JSON, or its criteria are invalid).
public interface IStoriesBacklogReads
{
    Task<BacklogViewDto> GetAsync(Guid projectId, BacklogMode mode, string? filter, bool includeCompleted, CancellationToken cancellationToken = default);
}

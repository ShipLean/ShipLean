namespace ShipLean.Contracts.Sprints;

// Metric semantics (stories and bugs only; task points never count; an item's points are its points now).
// Committed: points of the stories and bugs that were in the sprint at 00:00 UTC on its first day, as of then: an item that
//   rolls over later stays committed in the sprint it was planned into, and one that joined on or after the first day is
//   added, not committed.
// Completed: points of the stories and bugs that reached a Completed state between the sprint's first and last day while
//   in it, whenever they joined, and are still in a Completed state at the end of the sprint's last day (00:00 UTC the day
//   after) or at the moment they left the sprint, whichever comes first. Completed, then reopened and left open does not
//   count; completed, reopened and completed again counts once. An item finished after the sprint ended does not count for it,
//   and an item in a Removed state never counts as completed.
// Remaining: points of the items in the sprint at the end of a day that are neither completed nor removed by then (a late
//   joiner raises it from the day it joined; an item moved to a Removed state leaves it from that day, as if it had left the
//   sprint, but stays in Committed). Burndown: remaining at the end of each working day up to today, plus the ideal line
//   (committed down to zero across the working days).
// Velocity: the average Completed of the last Window ended sprints, or the project's override when set.
// Forecast: the unplanned backlog (stories and bugs not in a sprint, not finished), in backlog order, fills each future
//   sprint's remaining room: its team capacity when entered, else the velocity, minus the points already in it. Past the last
//   real sprint it continues into hypothetical sprints ("Sprint 27") at velocity. An item bigger than the room still lands
//   in an otherwise empty sprint. Unestimated items count zero points and are flagged.
public interface ISprintMetrics
{
    // Throws ApiException.NotFound (unknown sprint).
    Task<SprintMetricsDto> GetSprintAsync(Guid sprintId, CancellationToken cancellationToken = default);

    // Throws ApiException.NotFound (unknown project).
    Task<VelocityDto> GetVelocityAsync(Guid projectId, CancellationToken cancellationToken = default);

    // Every future sprint, then hypothetical ones until the whole backlog is placed (one per item when velocity is 0). Throws ApiException.NotFound (unknown project).
    Task<ForecastDto> GetForecastAsync(Guid projectId, CancellationToken cancellationToken = default);

    // The stories and bugs that were committed, added, completed or carried over in the sprint, in key order. Throws ApiException.NotFound (unknown sprint).
    Task<IReadOnlyList<SprintItemOutcomeDto>> GetSprintItemsAsync(Guid sprintId, CancellationToken cancellationToken = default);
}

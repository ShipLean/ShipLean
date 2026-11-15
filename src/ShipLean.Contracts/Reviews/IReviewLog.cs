using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Contracts.Reviews;

// The review events on tasks (SHIP-1465 D15, C4): append-only, one row per review a reviewer logs. Only a task can be reviewed;
// any other type is a validation error naming "task". The reviewer is the caller, stamped with the agent acting for them.
public interface IReviewLog
{
    Task<LoggedReview> AppendAsync(ReviewInput input, CancellationToken cancellationToken = default);
}

public sealed record ReviewInput(Guid WorkItemId, ReviewVerdict Verdict, string? Note);

// Round is the number of reviews on the task, this one included.
public sealed record LoggedReview(Guid Id, ReviewVerdict Verdict, DateTimeOffset At, int Round);

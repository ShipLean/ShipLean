namespace ShipLean.Application.Features.Mcp.Planning.Status;

// StartedAt is the task's first revision into an InProgress state, FinishedAt its last into Completed (UTC). The tool reports
// FinishedAt only while the task is Completed now. Commits are its url links PlanMarkers reads as commits, oldest first; ReviewRounds counts its live review comments.
public sealed record PlanTaskActivity(DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, IReadOnlyList<PlanCommitLink> Commits, int ReviewRounds);

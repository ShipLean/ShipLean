namespace ShipLean.Application.Features.Timeline.Facts;

// One commit of a task with a recorded time (SHIP-1398 K1, K8): a commit link's committed_at, or a readable repository commit's
// authored time. Commits with no time are not facts.
public sealed record TimelineCommitFact(string ItemKey, DateTimeOffset CommittedAt);

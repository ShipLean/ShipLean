namespace ShipLean.Application.Features.Timeline.Facts;

// One change to a task's Blocked tag (matched case-insensitively, D16): BlockedAfter is whether the task carries it after Rev.
public sealed record TimelineBlockedTagFact(string ItemKey, DateTimeOffset At, int Rev, bool BlockedAfter);

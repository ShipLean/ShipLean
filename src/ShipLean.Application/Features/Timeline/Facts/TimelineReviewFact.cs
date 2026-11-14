using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Facts;

// One review event on a task (C4, D15). The reviewer is the agent when AgentName is set, else the user ByUserId.
public sealed record TimelineReviewFact(
    string ItemKey,
    DateTimeOffset At,
    ReviewVerdict Verdict,
    string? Note,
    Guid ByUserId,
    string? AgentName);

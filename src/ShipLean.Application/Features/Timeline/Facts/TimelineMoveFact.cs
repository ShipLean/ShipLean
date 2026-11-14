using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Timeline.Facts;

// One state move of an item (the revision that set its state): the state it moved to, who moved it, and the agent's name when an
// agent made the move.
public sealed record TimelineMoveFact(
    string ItemKey,
    DateTimeOffset At,
    int Rev,
    string StateName,
    StateCategory Category,
    string? AgentName,
    Guid? UserId);

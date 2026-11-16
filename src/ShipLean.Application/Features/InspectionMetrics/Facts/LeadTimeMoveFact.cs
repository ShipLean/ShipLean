using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// One state move of a task (the revision that set its state): the state it moved to, who moved it, and the agent's name when an
// agent made the move.
public sealed record LeadTimeMoveFact(string ItemKey, DateTimeOffset At, int Rev, string StateName, StateCategory Category, string? AgentName, Guid? UserId);

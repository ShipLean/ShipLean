using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Mcp.Planning.Tree;

// One lead's share of a wave: a lane-less wave's only story, or one lane of a wave. Lane is null for a lane-less wave.
// Assignee is the person's display name. Rank is the backlog order key.
public sealed record PlanStory(
    Guid Id, string Key, string Title, WaveLabel Wave, string? Lane, string StateName, StateCategory Category, string? Assignee,
    string Rank, IReadOnlyList<PlanTask> Tasks);

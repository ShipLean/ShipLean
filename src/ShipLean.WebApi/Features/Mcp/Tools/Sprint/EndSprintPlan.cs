namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// What end_sprint seals: the sprint, where its unfinished items go ("next" or "backlog"), whether the end date moves to
// today first, and each item with the revision and destination the preview saw.
public sealed record EndSprintPlan(Guid SprintId, string To, bool EndToday, IReadOnlyList<EndSprintMove> Moves);

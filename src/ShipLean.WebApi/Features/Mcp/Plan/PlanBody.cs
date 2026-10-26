namespace ShipLean.WebApi.Features.Mcp.Plan;

// What a preview seals: the thing the apply will change (a sprint id, "capture", …) and the items with their revisions.
public sealed record PlanBody(string Target, IReadOnlyList<PlanItem> Items);

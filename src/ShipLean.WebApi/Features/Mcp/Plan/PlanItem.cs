namespace ShipLean.WebApi.Features.Mcp.Plan;

// One item a preview saw, at the revision it saw: the apply refuses when the item has moved on.
public sealed record PlanItem(Guid Id, int Revision);

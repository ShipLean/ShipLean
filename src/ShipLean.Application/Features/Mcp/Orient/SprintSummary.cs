namespace ShipLean.Application.Features.Mcp.Orient;

// Dates are ISO days (yyyy-MM-dd).
public sealed record SprintSummary(Guid Id, string Name, string Start, string End);

namespace ShipLean.Application.Features.Mcp.Status;

public sealed record SprintHead(string Name, string Goal, DateOnly StartDate, DateOnly EndDate);

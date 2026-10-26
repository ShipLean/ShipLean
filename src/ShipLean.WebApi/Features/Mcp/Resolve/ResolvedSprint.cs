namespace ShipLean.WebApi.Features.Mcp.Resolve;

public sealed record ResolvedSprint(Guid Id, string Name, bool Exact = true);

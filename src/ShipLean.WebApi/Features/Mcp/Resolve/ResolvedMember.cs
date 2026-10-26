namespace ShipLean.WebApi.Features.Mcp.Resolve;

public sealed record ResolvedMember(Guid Id, string Name, string Email, string Initials, bool Exact = true);

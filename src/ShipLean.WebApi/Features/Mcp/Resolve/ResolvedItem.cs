using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// A work item the user may act on at the role the caller asked for.
public sealed record ResolvedItem(Guid Id, string Key, Guid ProjectId, ProjectRole Role, bool Exact = true);

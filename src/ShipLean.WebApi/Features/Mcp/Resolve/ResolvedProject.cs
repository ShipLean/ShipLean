using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// A project the current user belongs to, with their role in it.
public sealed record ResolvedProject(Guid Id, string Key, string Name, ProjectRole Role, bool Exact = true);

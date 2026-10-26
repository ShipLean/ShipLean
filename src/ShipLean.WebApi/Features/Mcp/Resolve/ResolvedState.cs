using ShipLean.Contracts;
using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

public sealed record ResolvedState(Guid Id, string Name, WorkItemTypeId Type, StateCategory Category, bool Exact = true);

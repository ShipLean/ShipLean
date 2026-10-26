namespace ShipLean.Application.Features.Mcp.Planning.Tree;

// The plan's Feature. Goal is its Goal field ("" when unset); Description holds the ## Contracts section; SharedBranch is its
// branch: tag, null when it has none.
public sealed record PlanFeature(Guid Id, string ProjectKey, string Key, string Title, string Goal, string Description, string? SharedBranch);

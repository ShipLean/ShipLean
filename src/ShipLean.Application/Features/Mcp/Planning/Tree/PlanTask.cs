using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Mcp.Planning.Tree;

// One sub-agent's slice. Description is its brief; Tags are its parsed plan tags (model, ctx); Orphan is the orphan tag,
// set on the tasks a report's "left undone" items became.
public sealed record PlanTask(
    Guid Id, string Key, string Title, string Description, string StateName, StateCategory Category, string? Assignee,
    PlanTaskTags Tags, bool Orphan);

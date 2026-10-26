using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// A capture_work item with every name already resolved to an id: what a preview shows and what its sealed plan holds, so
// applying creates exactly this. ParentKey and SprintName are for reading aloud.
public sealed record PlannedItem(
    WorkItemTypeId Type,
    string Title,
    string? Description,
    IReadOnlyList<string> AcceptanceCriteria,
    string? ReproSteps,
    int? Severity,
    decimal? Points,
    IReadOnlyList<string> Tags,
    int? Priority,
    Guid? ParentId,
    string? ParentKey,
    Guid? SprintId,
    string? SprintName);

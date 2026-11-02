using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Backlog;

public sealed record BacklogAddItem(
    WorkItemTypeId Type, string Title, string? Description, decimal? Points, Guid? ParentId, IReadOnlyList<BacklogAddTask> Tasks);

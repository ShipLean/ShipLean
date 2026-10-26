using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Events;

public sealed record WorkItemCreated(Guid ProjectId, Guid Id, string Key, WorkItemTypeId WorkItemType, Guid? ParentId) : ProjectEvent(ProjectId);

using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// A task, story or bug of the Feature that never left New and was never closed. Parent is null for a story or bug.
public sealed record TimelineNotStartedDto(string Key, WorkItemTypeId Type, string Title, string? Parent);

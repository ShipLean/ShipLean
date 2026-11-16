using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Timeline.Facts;

// A task under one of the Feature's stories or bugs (ParentKey), with its current state by name and category.
public sealed record TimelineTaskFact(
    string Key,
    string ParentKey,
    string Title,
    Guid? AssigneeId,
    string StateName,
    StateCategory StateCategory);

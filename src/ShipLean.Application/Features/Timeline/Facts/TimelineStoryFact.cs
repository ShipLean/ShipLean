using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Timeline.Facts;

// A story or bug of the Feature (Type is Story or Bug), with its current state by name and category.
public sealed record TimelineStoryFact(
    string Key,
    WorkItemTypeId Type,
    string Title,
    Guid? AssigneeId,
    DateTimeOffset CreatedAt,
    string StateName,
    StateCategory StateCategory);

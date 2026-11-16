using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Timeline.Facts;

// The Feature itself: its current state by name and category, when and by whom it was created, and its epic when it has one.
public sealed record TimelineFeatureFact(
    Guid Id,
    string Key,
    string Title,
    string StateName,
    StateCategory StateCategory,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    TimelineEpicFact? Epic);

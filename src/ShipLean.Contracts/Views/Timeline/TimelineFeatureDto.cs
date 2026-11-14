using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// The Feature the timeline is about: its current state by name and category, and its epic when it has one.
public sealed record TimelineFeatureDto(Guid Id, string Key, string Title, string State, StateCategory StateCategory, TimelineEpicDto? Epic);

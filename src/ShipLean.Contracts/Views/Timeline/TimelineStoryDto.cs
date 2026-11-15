using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// A story's or bug's bracket (D3): from its first task going Active to its last task going Resolved. Type is Story or Bug.
// End is null while any task is open; Hours then runs to AsOf. Tasks are its task keys in bar order.
public sealed record TimelineStoryDto(
    string Key,
    WorkItemTypeId Type,
    string Title,
    TimelinePersonDto? Assignee,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    double? StartHours,
    double? EndHours,
    double? Hours,
    string? StartLabel,
    string? EndLabel,
    string? DurationLabel,
    IReadOnlyList<string> Tasks);

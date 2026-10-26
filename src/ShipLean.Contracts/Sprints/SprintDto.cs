using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

// DatesLabel "20 Oct – 2 Nov"; TimeLabel "9 days left" | "Ended 3 days ago" | "Starts in 5 days". Status is computed from the dates.
public sealed record SprintDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Goal, SprintStatus Status,
    RolloverStatus? RolloverStatus, string DatesLabel, string TimeLabel);

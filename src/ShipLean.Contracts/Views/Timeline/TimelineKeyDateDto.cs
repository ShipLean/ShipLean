namespace ShipLean.Contracts.Views.Timeline;

// One of the four key dates. At, DayLabel, By and Source are null until the point is reached; Source says what marked it
// ("Feature created", "Plan captured: 4 stories and bugs, 9 tasks", "SHIP-191 moved to Active", "Feature moved to Done").
public sealed record TimelineKeyDateDto(KeyDatePoint Point, DateTimeOffset? At, string? DayLabel, TimelinePersonDto? By, string? Source);

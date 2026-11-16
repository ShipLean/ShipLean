namespace ShipLean.Contracts.Views.Timeline;

// The execution the chart draws: from the first task going Active to the Feature's finish, or AsOf while it runs.
public sealed record TimelineExecutionDto(DateTimeOffset? Start, DateTimeOffset? End, double? CalendarDays);

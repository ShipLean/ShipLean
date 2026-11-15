namespace ShipLean.Application.Features.Timeline.Facts;

// A sprint of the project that overlaps the timeline's window, by its first and last day.
public sealed record TimelineSprintFact(string Name, DateOnly StartDate, DateOnly EndDate);

namespace ShipLean.Contracts.Views.Timeline;

// One review event logged on a task (D15).
public sealed record TimelineReviewDto(DateTimeOffset At, double AtHours, string AtLabel, ReviewVerdict Verdict, TimelinePersonDto By, string? Note);

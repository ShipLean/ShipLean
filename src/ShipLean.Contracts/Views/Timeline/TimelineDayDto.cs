namespace ShipLean.Contracts.Views.Timeline;

// One calendar day in the viewer's zone ("Mon 5 Oct"); a daylight-saving day is 23 or 25 hours long.
public sealed record TimelineDayDto(double StartHours, string Label, string DayOfMonth, bool Weekend, bool MonthStart);

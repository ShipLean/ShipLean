namespace ShipLean.Contracts.Views.Timeline;

// One calendar day in the viewer's zone ("Mon 5 Oct"); a daylight-saving day is 23 or 25 hours long. ShortLabel is the day without
// its weekday ("5 Oct") for the Fit week axis; WeekStart is true on a Monday.
public sealed record TimelineDayDto(double StartHours, string Label, string ShortLabel, string DayOfMonth, bool Weekend, bool MonthStart, bool WeekStart);

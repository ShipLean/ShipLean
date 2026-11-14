namespace ShipLean.Contracts.Views.Timeline;

// A project sprint that overlaps the drawn days, as a band on the axis.
public sealed record TimelineSprintDto(string Name, double StartHours, double EndHours);

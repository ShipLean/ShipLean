namespace ShipLean.Contracts.Demos;

// The timeline's execution: from the first task going Active to the Feature's finish. End is null while the Feature is open.
public sealed record SprintDemoExecution(DateTimeOffset? Start, DateTimeOffset? End, double? CalendarDays);

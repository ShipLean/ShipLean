namespace ShipLean.Contracts.Sprints;

// One working day of the burndown. Label ("2 Nov") and Day ("2", the axis tick) are built on the server for the sprint's UTC
// calendar day: sprint days are shared by every viewer, so they do not move with the viewer's zone.
public sealed record BurndownPoint(DateOnly Date, string Label, string Day, decimal Remaining, decimal Ideal);

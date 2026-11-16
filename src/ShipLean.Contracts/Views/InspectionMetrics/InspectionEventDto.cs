namespace ShipLean.Contracts.Views.InspectionMetrics;

// A point on the selected task's time: what happened, when, and a line of text (a commit's sha7, a review's verdict, a state).
public sealed record InspectionEventDto(InspectionEventKind Kind, DateTimeOffset At, string AtLabel, double Hours, string Text);

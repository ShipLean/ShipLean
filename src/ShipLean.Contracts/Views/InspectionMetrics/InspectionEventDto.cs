namespace ShipLean.Contracts.Views.InspectionMetrics;

// A point on the selected task's time: what happened, when, and a line of text (a commit's sha7, a review's verdict, a state).
// An event with At > the task's EndedAt (an approving review, the final resolve) happened after the lead time ended: the rail styles
// it as not counted. That is the rule; there is no flag.
public sealed record InspectionEventDto(InspectionEventKind Kind, DateTimeOffset At, string AtLabel, double Hours, string Text);

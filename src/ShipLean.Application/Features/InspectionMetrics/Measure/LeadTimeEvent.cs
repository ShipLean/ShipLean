using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

// A point on a measured task's time, with its line of text (a sha7, a state's name, a review's verdict).
public sealed record LeadTimeEvent(InspectionEventKind Kind, DateTimeOffset At, string Text);

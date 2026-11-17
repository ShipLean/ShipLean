using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// A review logged on a task (log_review).
public sealed record LeadTimeReviewFact(string ItemKey, DateTimeOffset At, ReviewVerdict Verdict);

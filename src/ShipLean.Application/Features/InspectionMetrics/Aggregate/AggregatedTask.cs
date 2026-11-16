using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

// A measured task with its facts and the sprint its lead time ended in (null when its end falls in no sprint or the weekend after
// one). LeadHours is unrounded, from start to end.
internal sealed record AggregatedTask(TaskLeadTime Lead, LeadTimeTaskFact Fact, InspectionSprint? Sprint, string Who, int Reviews, int Commits)
{
    public string Key => Fact.Key;

    public DateTimeOffset Start => Lead.Start ?? throw new InvalidOperationException($"{Fact.Key} is left out and has no lead time.");

    public double LeadHours => (Lead.End - Start).TotalHours;
}

using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

// A measured task with its facts and the sprint its lead time ended in (null when its end falls in no sprint or the weekend after
// one). LeadHours is unrounded, from start to end, and at least zero.
internal sealed record AggregatedTask(TaskLeadTime Lead, LeadTimeTaskFact Fact, InspectionSprint? Sprint, string Who, int Reviews, int Commits)
{
    public string Key => Fact.Key;

    public DateTimeOffset Start => Lead.Start ?? throw new InvalidOperationException($"{Fact.Key} is left out and has no lead time.");

    // Never below zero: a commit dated before the start would otherwise pull the means down.
    public double LeadHours => Math.Max(0, (Lead.End - Start).TotalHours);
}

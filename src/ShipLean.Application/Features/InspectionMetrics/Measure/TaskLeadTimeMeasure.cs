using ShipLean.Application.Features.InspectionMetrics.Facts;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

public sealed class TaskLeadTimeMeasure : ITaskLeadTimeMeasure
{
    // TODO: measure start, end and the active, blocked and rework split; until then no task is measured.
    public TaskLeadTime? Measure(LeadTimeTaskFact task, LeadTimeFacts facts) => null;
}

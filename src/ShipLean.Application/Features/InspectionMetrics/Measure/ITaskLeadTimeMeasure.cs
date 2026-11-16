using ShipLean.Application.Features.InspectionMetrics.Facts;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

// One task's lead time from the facts (K6), by the design's "How the numbers are read" and design D8/D9. Null when the task has
// not ended (no commit time and never resolved); a task that ended with no start recorded comes back with Start null and a
// LeftOutReason.
public interface ITaskLeadTimeMeasure
{
    TaskLeadTime? Measure(LeadTimeTaskFact task, LeadTimeFacts facts);
}

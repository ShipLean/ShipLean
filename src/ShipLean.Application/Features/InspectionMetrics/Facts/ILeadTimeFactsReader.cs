namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// The lead-time facts of a project (K5), read by B1. Sprints are not facts: the service passes them.
public interface ILeadTimeFactsReader
{
    // fromDay and toDay select tasks, not revisions: the tasks whose lead time could end in [fromDay, toDay + 2 days] (the design's
    // weekend grace after a sprint), meaning a first move to a Resolved-category state or a commit time inside that span. For each
    // such task it returns every state move, Blocked tag change, review and commit, whatever its date, so a task started before
    // fromDay keeps its start and is never read as left out.
    Task<LeadTimeFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct);
}

namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// The lead-time facts of a project between two days (K5), read by B1. Sprints are not facts: the service passes them.
public interface ILeadTimeFactsReader
{
    Task<LeadTimeFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct);
}

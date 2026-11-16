using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// The project and its live sprints, oldest first: what the service passes to the window and the aggregator (sprints are not facts).
public sealed record InspectionSprints(InspectionProjectDto Project, IReadOnlyList<InspectionSprint> Sprints);

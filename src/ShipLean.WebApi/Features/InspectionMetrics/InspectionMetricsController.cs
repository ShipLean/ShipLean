using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.InspectionMetrics;

// Inspection's lead time for a project (K3): ?range=last6|quarter|lastQuarter|all&cut=none|feature|who&period=<sprintId>&select=<taskKey>.
[Route("api/projects/{projectId:guid}/inspection-metrics")]
public sealed class InspectionMetricsController(IInspectionMetrics metrics) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InspectionMetricsViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<InspectionMetricsViewDto> Get(Guid projectId, [FromQuery] InspectionMetricsQuery query, CancellationToken cancellationToken)
        => metrics.GetAsync(projectId, query, cancellationToken);
}

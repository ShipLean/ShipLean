using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inspection;

[Route("api/projects/{projectId:guid}/usage/inspection")]
public sealed class InspectionUsageController(InspectionUsageReports reports) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Report(Guid projectId, InspectionUsageDto request, CancellationToken cancellationToken)
    {
        await reports.ReportAsync(projectId, request, cancellationToken);
        return NoContent();
    }
}

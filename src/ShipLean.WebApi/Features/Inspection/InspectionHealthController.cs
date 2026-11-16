using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inspection;

[Route("api/inspection/health")]
[InstanceAdmin]
public sealed class InspectionHealthController(InspectionHealthView health) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InspectionHealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<InspectionHealthDto> Get(CancellationToken cancellationToken) => health.GetAsync(cancellationToken);
}

using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inspection;

// Settings answer while the project is off; only the instance switch hides them. The PUT is project-admin only.
[Route("api/inspection/projects/{project}/settings")]
public sealed class InspectionSettingsController(InspectionSettingsService settings) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<InspectionProjectSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<InspectionProjectSettingsDto> Get(string project, CancellationToken cancellationToken) => settings.GetAsync(project, cancellationToken);

    [HttpPut]
    [ProducesResponseType<InspectionProjectSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<InspectionProjectSettingsDto> Put(string project, InspectionProjectSettingsDto request, CancellationToken cancellationToken)
        => settings.PutAsync(project, request, cancellationToken);
}

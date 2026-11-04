using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

[Route("api/projects/{id:guid}/demo-settings")]
public sealed class ProjectDemoSettingsController(IDemoSettings settings, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProjectDemoSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDemoSettingsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await settings.GetAsync(id, cancellationToken);
    }

    [HttpPut]
    [ProducesResponseType<ProjectDemoSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDemoSettingsDto> Put(Guid id, ProjectDemoSettingsPut put, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await settings.SaveAsync(id, put, cancellationToken);
    }
}

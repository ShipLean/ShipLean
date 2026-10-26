using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.ProjectSettings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.WebApi.Infrastructure;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Features.ProjectSettings;

[Route("api/projects/{id:guid}")]
public sealed class ProjectSettingsController(ProjectSettingsService settings, IProjectAccess access, ICurrentUser currentUser, IMemberRemovals removals) : ApiControllerBase
{
    [HttpGet("settings")]
    [ProducesResponseType<ProjectSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectSettingsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await settings.GetAsync(id, cancellationToken);
    }

    [HttpPut("settings")]
    [ProducesResponseType<ProjectSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ProjectSettingsDto> Put(Guid id, ProjectSettingsRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        var saved = await settings.SaveAsync(id, request.ToPut(), request.Key, cancellationToken);
        foreach (var userId in saved.RemovedUserIds)
        {
            await removals.RemovedAsync(id, userId);
        }

        return saved.Settings;
    }

    [HttpPost("leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        await settings.LeaveAsync(id, cancellationToken);
        await removals.RemovedAsync(id, currentUser.Id);
        return NoContent();
    }
}

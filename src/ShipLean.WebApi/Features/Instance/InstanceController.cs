using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Instance;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Instance;
using ShipLean.Contracts;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Instance;

// The instance admin's Instance General and Projects screens.
// GET  /api/instance/settings              -> InstanceSettingsDto
// PUT  /api/instance/settings              -> InstanceSettingsDto
// GET  /api/instance/projects              -> every project
// POST /api/instance/projects/{id}/join    -> 204; the caller becomes the project's Admin
[InstanceAdmin]
[Route("api/instance")]
public sealed class InstanceController(InstanceSettings settings, InstanceProjects projects, PublicOrigin origins,
    ICurrentUser currentUser, IUsageEvents events, TimeProvider time) : ApiControllerBase
{
    [HttpGet("settings")]
    [ProducesResponseType<InstanceSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<InstanceSettingsDto> GetSettings(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut("settings")]
    [ProducesResponseType<InstanceSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public async Task<InstanceSettingsDto> PutSettings(InstanceSettingsDto body, CancellationToken cancellationToken)
    {
        var saved = await settings.SaveAsync(body, cancellationToken);
        // A saved public origin is what agents open. A cluster service name stored as base_url is still not one.
        if (!RequestOrigin.HostIsClusterService(saved.BaseUrl))
        {
            origins.Clear();
        }
        return saved;
    }

    [HttpGet("projects")]
    [ProducesResponseType<IReadOnlyList<InstanceProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<InstanceProjectDto>> ListProjects(CancellationToken cancellationToken) => projects.ListAsync(cancellationToken);

    [HttpPost("projects/{id:guid}/join")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Join(Guid id, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await projects.JoinAsync(id, cancellationToken);
        SelfEmittedUsage.TrackOk(events, time, started, UsageEventNames.ProjectJoined, currentUser.Id, "api:Instance.Join", id);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Instance;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Instance;
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
public sealed class InstanceController(InstanceSettings settings, InstanceProjects projects) : ApiControllerBase
{
    [HttpGet("settings")]
    [ProducesResponseType<InstanceSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<InstanceSettingsDto> GetSettings(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut("settings")]
    [ProducesResponseType<InstanceSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<InstanceSettingsDto> PutSettings(InstanceSettingsDto body, CancellationToken cancellationToken) => settings.SaveAsync(body, cancellationToken);

    [HttpGet("projects")]
    [ProducesResponseType<IReadOnlyList<InstanceProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<InstanceProjectDto>> ListProjects(CancellationToken cancellationToken) => projects.ListAsync(cancellationToken);

    [HttpPost("projects/{id:guid}/join")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Join(Guid id, CancellationToken cancellationToken) => NoContentAfter(projects.JoinAsync(id, cancellationToken));
}

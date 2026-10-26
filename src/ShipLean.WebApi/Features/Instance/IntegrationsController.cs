using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Instance;

// The instance admin's Integrations tab: GitHub and GitLab tokens (never returned, only their last four) and the preview allow-list.
// GET /api/instance/integrations -> IntegrationsSettingsDto
// PUT /api/instance/integrations -> IntegrationsSettingsDto
[InstanceAdmin]
[Route("api/instance/integrations")]
public sealed class IntegrationsController(IntegrationsSettings settings) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IntegrationsSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IntegrationsSettingsDto> Get(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut]
    [ProducesResponseType<IntegrationsSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IntegrationsSettingsDto> Put(IntegrationsSaveDto body, CancellationToken cancellationToken) => settings.SaveAsync(body, cancellationToken);
}

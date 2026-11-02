using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Analytics;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Analytics;

// The instance admin's Analytics tab: which provider usage events go to.
// GET  /api/instance/analytics      -> AnalyticsSettingsDto
// PUT  /api/instance/analytics      -> AnalyticsSettingsDto
// POST /api/instance/analytics/test -> AnalyticsTestResultDto
[InstanceAdmin]
[Route("api/instance/analytics")]
public sealed class AnalyticsController(AnalyticsSettings settings) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AnalyticsSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AnalyticsSettingsDto> Get(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut]
    [ProducesResponseType<AnalyticsSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AnalyticsSettingsDto> Put(AnalyticsSettingsDto body, CancellationToken cancellationToken) => settings.SaveAsync(body, cancellationToken);

    [HttpPost("test")]
    [ProducesResponseType<AnalyticsTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AnalyticsTestResultDto> Test(CancellationToken cancellationToken) => settings.TestAsync(cancellationToken);
}

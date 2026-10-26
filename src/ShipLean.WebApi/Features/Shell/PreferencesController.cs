using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Shell;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Preferences;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Shell;

[Route("api/me/preferences")]
public sealed class PreferencesController(Preferences preferences) : ApiControllerBase
{
    // The stored document as { key, json }; 204 when there is no row or the value is JSON null.
    [HttpGet("{key}")]
    [ProducesResponseType<UserPreferenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        var preference = await preferences.GetAsync(key, cancellationToken);
        return preference is null ? NoContent() : Ok(preference);
    }

    [HttpPut("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public Task<NoContentResult> Put(string key, [FromBody] JsonElement value, CancellationToken cancellationToken)
        => NoContentAfter(preferences.PutAsync(key, value, cancellationToken));

    [HttpPost("reset-views")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<NoContentResult> ResetViews(CancellationToken cancellationToken) => NoContentAfter(preferences.ResetViewsAsync(cancellationToken));
}

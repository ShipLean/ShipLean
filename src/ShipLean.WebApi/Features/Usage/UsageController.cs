using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Usage;

[Route("api/usage")]
public sealed class UsageController : ApiControllerBase
{
    // TODO: check the screen against ScreenIds.All, check project access, and track screen.viewed; for now it accepts the report and does nothing.
    [HttpPost("screen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public IActionResult Screen(ScreenViewDto request) => NoContent();
}

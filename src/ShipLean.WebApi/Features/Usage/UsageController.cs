using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Usage;

[Route("api/usage")]
public sealed class UsageController(ScreenViews screenViews) : ApiControllerBase
{
    [HttpPost("screen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Screen(ScreenViewDto request, CancellationToken cancellationToken)
    {
        await screenViews.ReportAsync(request, cancellationToken);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Usage;
using ShipLean.Application.Features.WebVitals;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Usage;

[Route("api/usage")]
public sealed class UsageController(ScreenViews screenViews, WebVitalsReports webVitals, WebVitalsConfigReader webVitalsConfig) : ApiControllerBase
{
    public const int WebVitalsMaxBodyBytes = 16_384;

    [HttpPost("screen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Screen(ScreenViewDto request, CancellationToken cancellationToken)
    {
        await screenViews.ReportAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("web-vitals/config")]
    [ProducesResponseType<WebVitalsConfigDto>(StatusCodes.Status200OK)]
    public Task<WebVitalsConfigDto> WebVitalsConfig(CancellationToken cancellationToken) => webVitalsConfig.GetAsync(cancellationToken);

    [HttpPost("web-vitals")]
    [RequestSizeLimit(WebVitalsMaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public NoContentResult WebVitals(WebVitalsBatchDto request)
    {
        webVitals.Report(request, Request.Headers.UserAgent.ToString());
        return NoContent();
    }
}

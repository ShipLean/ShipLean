using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WebVitals;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WebVitals;

// The SPA's web vitals: whether to measure them, and the readings. Under api/usage with the screen views, in its own feature so it
// references only the web vitals pipeline.
[Route("api/usage/web-vitals")]
public sealed class WebVitalsController(WebVitalsReports reports, WebVitalsConfigReader config) : ApiControllerBase
{
    public const int MaxBodyBytes = 16_384;

    [HttpGet("config")]
    [ProducesResponseType<WebVitalsConfigDto>(StatusCodes.Status200OK)]
    public Task<WebVitalsConfigDto> Config(CancellationToken cancellationToken) => config.GetAsync(cancellationToken);

    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public NoContentResult Post(WebVitalsBatchDto request)
    {
        reports.Report(request, Request.Headers.UserAgent.ToString());
        return NoContent();
    }
}

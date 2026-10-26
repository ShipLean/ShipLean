using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Webhooks;

// GitHub pull_request and GitLab Merge Request Hook deliveries (#39). Anonymous: the provider proves itself with the
// shared secret (WebhookReceiver). Each endpoint is 404 while its SHIPLEAN_*_WEBHOOK_SECRET is unset.
// POST /api/webhooks/github  -> WebhookResultDto
// POST /api/webhooks/gitlab  -> WebhookResultDto
[AllowAnonymous]
[Route("api/webhooks")]
[EnableRateLimiting(WebhookServices.RateLimit)]
[RequestSizeLimit(WebhookServices.MaxBodyBytes)]
public sealed class WebhooksController(WebhookReceiver receiver) : ApiControllerBase
{
    [HttpPost("github")]
    [ProducesResponseType<WebhookResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<WebhookResultDto> GitHub(CancellationToken cancellationToken) => receiver.ReceiveAsync(WebhookProvider.Github, Request, cancellationToken);

    [HttpPost("gitlab")]
    [ProducesResponseType<WebhookResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<WebhookResultDto> GitLab(CancellationToken cancellationToken) => receiver.ReceiveAsync(WebhookProvider.Gitlab, Request, cancellationToken);
}

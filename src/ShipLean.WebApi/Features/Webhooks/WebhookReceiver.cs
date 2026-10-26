using Agoda.IoC.Core;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Features.Webhooks;

// The HTTP side of a delivery: the endpoint exists only while the provider's secret is set (404 otherwise), the body is
// read whole (RequestSizeLimit caps it), and the signature (GitHub) or token (GitLab) must match, or it is 401 and logged
// without the body. Then PullRequestWebhooks does the work.
[RegisterPerRequest]
public sealed class WebhookReceiver(IConfiguration configuration, PullRequestWebhooks webhooks, ILogger<WebhookReceiver> logger)
{
    public const string GithubEventHeader = "X-GitHub-Event";
    public const string GithubDeliveryHeader = "X-GitHub-Delivery";
    public const string GitlabEventHeader = "X-Gitlab-Event";
    public const string GitlabDeliveryHeader = "X-Gitlab-Event-UUID";

    public async Task<WebhookResultDto> ReceiveAsync(WebhookProvider provider, HttpRequest request, CancellationToken cancellationToken)
    {
        var secret = WebhookSecrets.For(provider, configuration) ?? throw ApiException.NotFound("Not found.");
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);

        var github = provider == WebhookProvider.Github;
        var deliveryId = Header(request, github ? GithubDeliveryHeader : GitlabDeliveryHeader);
        var valid = github
            ? WebhookSecrets.GithubSignatureMatches(secret, Header(request, WebhookSecrets.GithubSignatureHeader), body.Span)
            : WebhookSecrets.GitlabTokenMatches(secret, Header(request, WebhookSecrets.GitlabTokenHeader));
        if (!valid)
        {
            logger.LogWarning("Refused a {Provider} webhook delivery {DeliveryId} from {RemoteIp}: bad or missing signature", provider, deliveryId, request.HttpContext.Connection.RemoteIpAddress);
            throw ApiException.Unauthorized("The webhook signature is missing or does not match.");
        }

        return await webhooks.ReceiveAsync(provider, deliveryId, Header(request, github ? GithubEventHeader : GitlabEventHeader), body, cancellationToken);
    }

    private static string? Header(HttpRequest request, string name)
        => request.Headers.TryGetValue(name, out var value) && value.Count == 1 && !string.IsNullOrWhiteSpace(value[0]) ? value[0] : null;
}

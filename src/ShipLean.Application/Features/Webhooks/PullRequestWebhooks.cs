using System.Text.Json;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// A delivery whose signature has been checked: a delivery id seen before is a no-op; otherwise the provider's parser
// reads the payload, PullRequestSync applies a pull or merge request event, IIssueRouter sends a GitHub issue or issue
// comment to its lane, and the delivery is recorded (after the work, so a delivery that failed half way is done again when
// the provider retries it; every step is safe to repeat, and the issue lanes don't rely on this dedupe).
[RegisterPerRequest]
public sealed class PullRequestWebhooks(NpgsqlDataSource dataSource, PullRequestSync sync, IIssueRouter issues, IUsageEvents usage, TimeProvider time)
{
    public async Task<WebhookResultDto> ReceiveAsync(WebhookProvider provider, string? deliveryId, string? eventName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (deliveryId is not null && await SeenAsync(provider, deliveryId, cancellationToken))
        {
            return WebhookResultDto.Of(WebhookOutcome.Duplicate);
        }

        var parsed = Parse(provider, eventName, body);
        var result = parsed switch
        {
            { Event: { } pullRequest } => await sync.ApplyAsync(pullRequest, cancellationToken),
            { Issue: { } issue } => await issues.RouteAsync(issue, cancellationToken),
            { Comment: { } comment } => await issues.RouteAsync(comment, cancellationToken),
            _ => WebhookResultDto.Of(parsed.Outcome),
        };
        if (deliveryId is not null)
        {
            await RecordAsync(provider, deliveryId, eventName ?? string.Empty, parsed, cancellationToken);
        }
        if (parsed.Event is { } processed)
        {
            await TrackReceivedAsync(provider, processed.Url, cancellationToken);
        }
        return result;
    }

    // After the work and the record: the projects of the items that now link the pull or merge request (none when no item matched).
    // The event is analytics, so a failure to look the projects up is never the delivery's failure.
    private async Task TrackReceivedAsync(WebhookProvider provider, string url, CancellationToken cancellationToken)
    {
        Guid[] projects = [];
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            projects = [.. await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT DISTINCT w.project_id FROM work_item_link l JOIN work_item w ON w.id = l.source_id
                WHERE l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) = link_url_key(@url)
                """,
                new { url }, cancellationToken: cancellationToken))];
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            projects = [];
        }

        usage.Track(new UsageEvent(
            UsageEventNames.WebhookReceived, UsageSurface.System, UsageActor.System, projects.Length == 1 ? projects[0] : null, projects.Length, UsageOutcome.Ok,
            null, null, $"system:{nameof(PullRequestWebhooks)}", time.GetUtcNow(), new Dictionary<string, object> { [UsageProps.Provider] = ProviderDb(provider) }));
    }

    private static ParsedDelivery Parse(WebhookProvider provider, string? eventName, ReadOnlyMemory<byte> body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw ApiException.Validation("Send the webhook payload as application/json.");
        }
        using (document)
        {
            return provider == WebhookProvider.Github
                ? GitHubPayload.Parse(eventName, document.RootElement)
                : GitLabPayload.Parse(eventName, document.RootElement);
        }
    }

    private async Task<bool> SeenAsync(WebhookProvider provider, string deliveryId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM webhook_delivery WHERE provider = @provider AND delivery_id = @deliveryId)",
            new { provider = ProviderDb(provider), deliveryId }, cancellationToken: cancellationToken));
    }

    private async Task RecordAsync(WebhookProvider provider, string deliveryId, string eventName, ParsedDelivery parsed, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO webhook_delivery (provider, delivery_id, event, repository, url)
            VALUES (@provider, @deliveryId, @eventName, @repository, @url)
            ON CONFLICT (provider, delivery_id) DO NOTHING
            """,
            new { provider = ProviderDb(provider), deliveryId, eventName, repository = parsed.Repository, url = parsed.Url },
            cancellationToken: cancellationToken));
    }

    private static string ProviderDb(WebhookProvider provider) => provider == WebhookProvider.Github ? "github" : "gitlab";
}

using System.Text.Json;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// A delivery whose signature has been checked: a delivery id seen before is a no-op; otherwise the provider's parser
// reads the payload, PullRequestSync applies a pull or merge request event, and the delivery is recorded (after the work,
// so a delivery that failed half way is done again when the provider retries it; every step is safe to repeat).
[RegisterPerRequest]
public sealed class PullRequestWebhooks(NpgsqlDataSource dataSource, PullRequestSync sync)
{
    public async Task<WebhookResultDto> ReceiveAsync(WebhookProvider provider, string? deliveryId, string? eventName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (deliveryId is not null && await SeenAsync(provider, deliveryId, cancellationToken))
        {
            return WebhookResultDto.Of(WebhookOutcome.Duplicate);
        }

        var parsed = Parse(provider, eventName, body);
        var result = parsed.Event is { } pullRequest ? await sync.ApplyAsync(pullRequest, cancellationToken) : WebhookResultDto.Of(parsed.Outcome);
        if (deliveryId is not null)
        {
            await RecordAsync(provider, deliveryId, eventName ?? string.Empty, parsed, cancellationToken);
        }
        return result;
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
            new { provider = ProviderDb(provider), deliveryId, eventName, repository = parsed.Repository, url = parsed.Event?.Url },
            cancellationToken: cancellationToken));
    }

    private static string ProviderDb(WebhookProvider provider) => provider == WebhookProvider.Github ? "github" : "gitlab";
}

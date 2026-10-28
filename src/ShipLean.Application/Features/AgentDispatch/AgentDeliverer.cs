using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// Sends the agent_delivery outbox, the way Linear sends a webhook (headers checked against https://linear.app/developers/webhooks).
// A pass (RunDueAsync) claims the due row of one session at a time with FOR UPDATE SKIP LOCKED and a lease (claimed_until), only
// ever a session's lowest pending row, so a session's rows go out strictly in seq order and two instances never send the same row.
// webhookTimestamp and webhookId go into the body at send time and the signature covers exactly the bytes sent.
// A 200 is sent; anything else (another status, a redirect, a timeout, no connection) is a failure and follows AgentDeliverySchedule.
// Only a `created` that runs out of retries fails its session; a `prompted` that does is marked failed and logged, and the session carries on.
// A send is at least once: a row whose "sent" mark could not be written is sent again, so receivers dedupe on Linear-Delivery (the relay does).
// The hosted loop is AgentDeliveryService; tests drive a pass directly with their own clock.
public sealed class AgentDeliverer(
    NpgsqlDataSource dataSource, IAgentConnections connections, IHttpClientFactory httpClients, AgentSessionNotifier notifier, TimeProvider time, ILogger<AgentDeliverer> logger)
{
    public const string HttpClientName = "agent-delivery";
    public const string EventHeaderValue = "AgentSessionEvent";

    // A row's lease: longer than the 5 s request, so a sender that died frees the row soon.
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    // The agent has this long after Stop to answer before the session is canceled.
    private static readonly TimeSpan StopGrace = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions BodyOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Sends every row that is due now. Returns how many it tried.
    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        var tried = 0;
        while (await ClaimAsync(cancellationToken) is { } row)
        {
            await DeliverAsync(row, cancellationToken);
            tried++;
        }

        return tried;
    }

    // A session that was told to stop and has not ended by itself after StopGrace is canceled; its unsent deliveries are dropped.
    // One idempotent UPDATE, safe on every instance.
    public async Task<int> CancelUnansweredStopsAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var canceled = (await connection.QueryAsync<Guid>(new CommandDefinition(
            $"""
            WITH ended AS (
                UPDATE agent_session SET state = 'canceled', ended_at = @now
                WHERE state IN {AgentSessionStates.LiveSql} AND stop_requested_at <= @cutoff
                RETURNING id),
            dropped AS (
                UPDATE agent_delivery d SET status = 'failed', claimed_until = NULL, last_error = 'The session was canceled.'
                FROM ended WHERE d.session_id = ended.id AND d.status = 'pending')
            SELECT id FROM ended
            """,
            new { now, cutoff = now - StopGrace }, cancellationToken: cancellationToken))).ToList();
        foreach (var id in canceled)
        {
            logger.LogInformation("Agent session {SessionId} was canceled: the agent did not answer Stop in time", id);
            await notifier.ChangedAsync(id);
        }

        return canceled.Count;
    }

    private async Task<AgentDeliveryRow?> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AgentDeliveryRow>(new CommandDefinition(
            """
            WITH due AS (
                SELECT d.id FROM agent_delivery d
                WHERE d.status = 'pending' AND d.next_attempt_at <= @now AND (d.claimed_until IS NULL OR d.claimed_until <= @now)
                  AND d.seq = (SELECT min(x.seq) FROM agent_delivery x WHERE x.session_id = d.session_id AND x.status = 'pending')
                ORDER BY d.next_attempt_at, d.seq
                LIMIT 1
                FOR UPDATE SKIP LOCKED),
            claimed AS (
                UPDATE agent_delivery d SET claimed_until = @until FROM due WHERE d.id = due.id
                RETURNING d.id, d.session_id, d.seq, d.action, d.body::text AS body, d.attempts)
            SELECT c.id, c.session_id AS sessionId, c.seq, c.action, c.body, c.attempts, s.connection_id AS connectionId, s.state AS sessionState
            FROM claimed c JOIN agent_session s ON s.id = c.session_id
            """,
            new { now, until = now + Lease }, cancellationToken: cancellationToken));
    }

    private async Task DeliverAsync(AgentDeliveryRow row, CancellationToken cancellationToken)
    {
        if (row.Action == "prompted" && AgentSessionStates.IsTerminal(row.SessionState))
        {
            await DropAsync(row, cancellationToken);
            return;
        }

        AgentConnection? agent = null;
        string? error;
        try
        {
            agent = await connections.GetAsync(row.ConnectionId, cancellationToken);
            error = agent is null ? "The agent connection no longer exists." : await SendAsync(row, agent, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            error = "No answer within 5 seconds.";
        }
        catch (Exception failed)
        {
            error = $"{failed.GetType().Name}: {failed.Message}";
        }

        if (error is null)
        {
            await MarkSentAsync(row, cancellationToken);
            return;
        }

        await FailedAsync(row, agent?.Name ?? row.ConnectionId.ToString(), error, cancellationToken);
    }

    // Null when the receiver answered 200.
    private async Task<string?> SendAsync(AgentDeliveryRow row, AgentConnection agent, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(agent.ReceiverUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return "The receiver URL is not an http(s) address.";
        }

        var sentAt = time.GetUtcNow().ToUnixTimeMilliseconds();
        var body = JsonNode.Parse(row.Body)!.AsObject();
        body["webhookTimestamp"] = sentAt;
        body["webhookId"] = agent.Id.ToString();
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString(BodyOptions));

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
        request.Headers.TryAddWithoutValidation("Linear-Delivery", row.Id.ToString());
        request.Headers.TryAddWithoutValidation("Linear-Event", EventHeaderValue);
        request.Headers.TryAddWithoutValidation(LinearSignature.Header, LinearSignature.Sign(agent.SigningSecret, bytes));
        request.Headers.TryAddWithoutValidation("Linear-Timestamp", sentAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.UserAgent.ParseAdd("Linear-Webhook");

        using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.StatusCode == HttpStatusCode.OK ? null : $"The receiver answered {(int)response.StatusCode} {response.StatusCode}.";
    }

    // The receiver has the delivery already, so a failed write is tried once more before it is left pending: left pending, the row is
    // sent again, which is why receivers must dedupe on the Linear-Delivery header (the relay does).
    private async Task MarkSentAsync(AgentDeliveryRow row, CancellationToken cancellationToken)
    {
        try
        {
            await WriteSentAsync(row, cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogWarning(failed, "Could not record agent delivery {DeliveryId} as sent; trying once more", row.Id);
            await WriteSentAsync(row, cancellationToken);
        }
    }

    private async Task WriteSentAsync(AgentDeliveryRow row, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_delivery SET status = 'sent', attempts = attempts + 1, claimed_until = NULL, last_error = NULL WHERE id = @id AND status = 'pending'",
            new { id = row.Id }, cancellationToken: cancellationToken));
    }

    private async Task DropAsync(AgentDeliveryRow row, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_delivery SET status = 'failed', claimed_until = NULL, last_error = 'The session had already ended.' WHERE id = @id AND status = 'pending'",
            new { id = row.Id }, cancellationToken: cancellationToken));
    }

    private async Task FailedAsync(AgentDeliveryRow row, string connectionName, string error, CancellationToken cancellationToken)
    {
        var attempts = row.Attempts + 1;
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (AgentDeliverySchedule.RetryAfter(row.Action, attempts) is { } wait)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE agent_delivery SET attempts = @attempts, next_attempt_at = @next, claimed_until = NULL, last_error = @error WHERE id = @id AND status = 'pending'",
                new { id = row.Id, attempts, next = now + wait, error }, cancellationToken: cancellationToken));
            logger.LogWarning(
                "Agent delivery {DeliveryId} ({Action}, try {Attempt}) to connection {Connection} ({ConnectionId}) failed: {Error} Trying again in {Wait}.",
                row.Id, row.Action, attempts, connectionName, row.ConnectionId, error, wait);
            return;
        }

        if (row.Action != "created")
        {
            // A follow-up the receiver never took is lost, but the session it was for is still running: only a `created` that
            // never arrives fails the session, because then no agent ever heard of it.
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE agent_delivery SET status = 'failed', attempts = @attempts, claimed_until = NULL, last_error = @error WHERE id = @id AND status = 'pending'",
                new { id = row.Id, attempts, error }, cancellationToken: cancellationToken));
            logger.LogWarning(
                "Agent delivery {DeliveryId} ({Action}, try {Attempt}) to connection {Connection} ({ConnectionId}) failed for good: {Error} Session {SessionId} is left running.",
                row.Id, row.Action, attempts, connectionName, row.ConnectionId, error, row.SessionId);
            await notifier.ChangedAsync(row.SessionId);
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_delivery SET status = 'failed', attempts = @attempts, claimed_until = NULL, last_error = @error WHERE id = @id AND status = 'pending'",
            new { id = row.Id, attempts, error }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE agent_session SET state = 'failed', ended_at = @now WHERE id = @sessionId AND state IN {AgentSessionStates.LiveSql}",
            new { sessionId = row.SessionId, now }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_delivery SET status = 'failed', claimed_until = NULL, last_error = 'The session failed.' WHERE session_id = @sessionId AND status = 'pending'",
            new { sessionId = row.SessionId }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        logger.LogWarning(
            "Agent delivery {DeliveryId} ({Action}, try {Attempt}) to connection {Connection} ({ConnectionId}) failed for good: {Error} Session {SessionId} failed.",
            row.Id, row.Action, attempts, connectionName, row.ConnectionId, error, row.SessionId);
        await notifier.ChangedAsync(row.SessionId);
    }
}

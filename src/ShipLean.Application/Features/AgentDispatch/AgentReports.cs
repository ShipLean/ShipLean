using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Webhooks;

namespace ShipLean.Application.Features.AgentDispatch;

// IAgentReports: what an agent writes back (Half 2 of the dispatch protocol). Each write runs in one transaction that holds the
// session row's lock (SELECT ... FOR UPDATE), the same lock AgentPrompter takes, so two reports (or a report and a Stop) on one
// session never interleave. Every query names the connection, so another connection's session is simply not found.
//
// State follows the last activity (provider doc, Half 2): thought and action are working, an elicitation is input required, a
// response is completed (canceled when Stop was requested, which is how an agent answers Stop) and an error is failed. A session
// that has already ended keeps its state (the activity is stored all the same); a stale one moves again unless the item has since
// started another session. An ephemeral activity (thought and action only) is deleted when the session's next activity arrives.
// The body of a response or error is also posted as a comment, written as the user who started the session and stamped with the
// connection name, so it never becomes a `prompted` webhook. The session changed event goes out after each commit.
public sealed class AgentReports(
    NpgsqlDataSource dataSource,
    ICommentStore comments,
    IPullRequestLinks pullRequestLinks,
    AgentSessionNotifier notifier,
    TimeProvider time,
    ILogger<AgentReports> logger) : IAgentReports
{
    private const string Thought = "thought";
    private const string Action = "action";
    private const string Elicitation = "elicitation";
    private const string Response = "response";
    private const string Error = "error";

    private static readonly string[] Types = [Thought, Action, Elicitation, Response, Error];
    private static readonly string[] PlanStatuses = ["pending", "inProgress", "completed", "canceled"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Guid> CreateActivityAsync(AgentReporter reporter, AgentActivityCreate create, CancellationToken cancellationToken = default)
    {
        Validate(create);
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LockAsync(connection, transaction, reporter, create.SessionId, cancellationToken);

        var id = create.Id ?? Guid.CreateVersion7();
        if (create.Id is not null)
        {
            var owner = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT session_id FROM agent_activity WHERE id = @id", new { id }, transaction, cancellationToken: cancellationToken));
            if (owner == create.SessionId)
            {
                return id;
            }
            if (owner is not null)
            {
                throw ApiException.Validation("That activity id is already used.", "id");
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM agent_activity WHERE session_id = @sessionId AND ephemeral", new { sessionId = create.SessionId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO agent_activity (id, session_id, type, body, action, parameter, result, ephemeral, signal, created_at)
            VALUES (@id, @sessionId, @type, @body, @action, @parameter, @result, @ephemeral, @signal, @now)
            """,
            new { id, sessionId = create.SessionId, type = create.Type, body = create.Body, action = create.Action, parameter = create.Parameter, result = create.Result, ephemeral = create.Ephemeral, signal = create.Signal, now },
            transaction, cancellationToken: cancellationToken));
        await MoveStateAsync(connection, transaction, session, create, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(create.SessionId);

        if (create.Type is Response or Error)
        {
            await PostCommentAsync(reporter, session, create.Body!, cancellationToken);
        }

        return id;
    }

    public async Task UpdateSessionAsync(AgentReporter reporter, Guid sessionId, AgentSessionChange change, CancellationToken cancellationToken = default)
    {
        ValidateChange(change);
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var session = await LockAsync(connection, transaction, reporter, sessionId, cancellationToken);

        var before = Parse(session.ExternalUrls);
        var urls = (change.ExternalUrls ?? before).ToList();
        foreach (var added in change.Added ?? [])
        {
            var at = urls.FindIndex(u => u.Url == added.Url);
            if (at >= 0)
            {
                urls[at] = added;
            }
            else
            {
                urls.Add(added);
            }
        }
        urls.RemoveAll(u => change.Removed?.Contains(u.Url) == true);
        var plan = change.Plan is null ? session.Plan : JsonSerializer.Serialize(change.Plan.Select(p => new { content = p.Content, status = p.Status }), Json);

        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE agent_session
            SET external_urls = @urls::jsonb, plan = @plan::jsonb,
                last_activity_at = CASE WHEN state IN {AgentSessionStates.TerminalSql} THEN last_activity_at ELSE @now END
            WHERE id = @id
            """,
            new { id = sessionId, urls = JsonSerializer.Serialize(urls.Select(u => new { url = u.Url, label = u.Label }), Json), plan, now },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(sessionId);

        var known = before.Select(u => u.Url).ToHashSet();
        foreach (var url in urls.Where(u => !known.Contains(u.Url)))
        {
            await LinkPullRequestAsync(reporter, session.WorkItemId, url, cancellationToken);
        }
    }

    public async Task<AgentComment> CreateCommentAsync(AgentReporter reporter, string issue, string body, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var session = await connection.QuerySingleOrDefaultAsync<SessionOnItem>(new CommandDefinition(
            """
            SELECT s.work_item_id AS workItemId, s.created_by AS createdBy
            FROM agent_session s JOIN work_item w ON w.id = s.work_item_id
            WHERE s.connection_id = @connectionId AND (w.id = @id OR w.key = @key)
            ORDER BY s.created_at DESC LIMIT 1
            """,
            new { connectionId = reporter.ConnectionId, id = Guid.TryParse(issue, out var parsed) ? parsed : Guid.Empty, key = issue.Trim().ToUpperInvariant() },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("Issue not found.");
        var added = await comments.AddAsAgentAsync(session.WorkItemId, new CommentCreate(body), reporter.Name, session.CreatedBy, cancellationToken);
        return new AgentComment(added.Id, added.WorkItemId, added.Body, added.CreatedAt);
    }

    private static async Task<LockedSession> LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AgentReporter reporter, Guid sessionId, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<LockedSession>(new CommandDefinition(
            """
            SELECT work_item_id AS workItemId, state, stop_requested_at AS stopRequestedAt, created_by AS createdBy,
                   external_urls::text AS externalUrls, plan::text AS plan
            FROM agent_session WHERE id = @sessionId AND connection_id = @connectionId FOR UPDATE
            """,
            new { sessionId, connectionId = reporter.ConnectionId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("Agent session not found.");

    private static async Task MoveStateAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, LockedSession session, AgentActivityCreate create, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (AgentSessionStates.IsTerminal(session.State))
        {
            return;
        }

        var next = create.Type switch
        {
            Elicitation => AgentSessionStates.InputRequired,
            Response => session.StopRequestedAt is null ? AgentSessionStates.Completed : AgentSessionStates.Canceled,
            Error => AgentSessionStates.Failed,
            _ => AgentSessionStates.Working,
        };
        var ended = AgentSessionStates.IsTerminal(next) ? now : (DateTimeOffset?)null;
        // A stale session whose item has started another one stays stale (the unique index allows one live session), but still records the activity time.
        var moved = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE agent_session SET state = @next, last_activity_at = @now, ended_at = @ended
            WHERE id = @id
              AND (state <> 'stale' OR @next NOT IN ('working', 'inputRequired')
                   OR NOT EXISTS (SELECT 1 FROM agent_session o WHERE o.work_item_id = @workItemId AND o.id <> @id AND o.state IN {AgentSessionStates.LiveSql}))
            """,
            new { id = create.SessionId, workItemId = session.WorkItemId, next, now, ended }, transaction, cancellationToken: cancellationToken));
        if (moved == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE agent_session SET last_activity_at = @now WHERE id = @id", new { id = create.SessionId, now }, transaction, cancellationToken: cancellationToken));
        }
    }

    // The activity is committed already, so a comment that cannot be written (a body over the limit) is logged, not thrown back
    // at an agent whose report did land.
    private async Task PostCommentAsync(AgentReporter reporter, LockedSession session, string body, CancellationToken cancellationToken)
    {
        try
        {
            await comments.AddAsAgentAsync(session.WorkItemId, new CommentCreate(body), reporter.Name, session.CreatedBy, cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogWarning(failed, "Could not post an agent's response as a comment on item {WorkItemId}", session.WorkItemId);
        }
    }

    private async Task LinkPullRequestAsync(AgentReporter reporter, Guid workItemId, AgentExternalUrl url, CancellationToken cancellationToken)
    {
        try
        {
            await pullRequestLinks.LinkAsync(workItemId, url.Url, url.Label, $"agent {reporter.Name}", cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not link {Url} to item {WorkItemId}", url.Url, workItemId);
        }
    }

    private static void Validate(AgentActivityCreate create)
    {
        if (create.Type == "prompt")
        {
            throw ApiException.Validation("An agent cannot create a prompt activity; only people prompt.", "content");
        }
        if (!Types.Contains(create.Type))
        {
            throw ApiException.Validation($"Activity type must be one of {string.Join(", ", Types)}.", "content");
        }
        if (create.Ephemeral && create.Type is not (Thought or Action))
        {
            throw ApiException.Validation("Only a thought or an action can be ephemeral.", "ephemeral");
        }
        if (create.Type == Action ? string.IsNullOrWhiteSpace(create.Action) : string.IsNullOrWhiteSpace(create.Body))
        {
            throw ApiException.Validation(create.Type == Action ? "An action needs an action." : $"A {create.Type} needs a body.", "content");
        }
    }

    private static void ValidateChange(AgentSessionChange change)
    {
        var bad = (change.ExternalUrls ?? []).Concat(change.Added ?? []).FirstOrDefault(u => !IsWebUrl(u.Url));
        if (bad is not null)
        {
            throw ApiException.Validation($"'{bad.Url}' is not an http or https URL.", "externalUrls");
        }
        if ((change.Plan ?? []).Any(s => string.IsNullOrWhiteSpace(s.Content) || !PlanStatuses.Contains(s.Status)))
        {
            throw ApiException.Validation($"A plan step needs content and a status of {string.Join(", ", PlanStatuses)}.", "plan");
        }
    }

    private static bool IsWebUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static List<AgentExternalUrl> Parse(string json)
        => [.. (JsonSerializer.Deserialize<List<StoredUrl>>(json, Json) ?? []).Select(u => new AgentExternalUrl(u.Label ?? string.Empty, u.Url))];

    private sealed record StoredUrl(string Url, string? Label);

    private sealed record LockedSession(Guid WorkItemId, string State, DateTimeOffset? StopRequestedAt, Guid CreatedBy, string ExternalUrls, string Plan);

    private sealed record SessionOnItem(Guid WorkItemId, Guid CreatedBy);
}

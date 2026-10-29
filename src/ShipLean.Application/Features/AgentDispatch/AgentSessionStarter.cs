using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.AgentDispatch;

// Opens an agent session: one transaction inserts the session (submitted) and its `created` delivery, rendered by
// AgentContextRenderer. The partial unique index ux_agent_session_live decides a race, so a second starter finds the item
// taken and writes nothing. Nothing is sent here: the delivery service sends the outbox. An archived connection counts as no connection.
public sealed class AgentSessionStarter(
    NpgsqlDataSource dataSource, IProjectAgentSettings projectSettings, IAgentConnections connections, AgentSessionNotifier notifier, TimeProvider time, IBlobStore blobs, ILogger<AgentSessionStarter> logger)
{
    // agent-relay refuses a body over 1 MiB (413, then retries until the session fails), so the body is kept under this.
    private const int MaxBodyBytes = 900_000;

    private readonly AgentContextLoader _loader = new(dataSource, blobs, logger);

    public async Task<AgentStartPlan> PlanAsync(Guid workItemId, Guid? connectionId, string? prompt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projectId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That work item was not found.");
        var settings = await projectSettings.GetAsync(projectId, cancellationToken);
        // connectionId overrides the project's connection for every check below; a blank prompt is no prompt.
        prompt = string.IsNullOrWhiteSpace(prompt) ? null : prompt;
        var agent = (connectionId ?? settings.ConnectionId) is { } id ? await connections.GetAsync(id, cancellationToken) : null;
        if (agent is null || agent.ArchivedAt is not null)
        {
            return new AgentStartPlan(AgentStartBlock.NoConnection, Guid.Empty, string.Empty, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(settings.RepoUrl))
        {
            return new AgentStartPlan(AgentStartBlock.NoRepository, agent.Id, string.Empty, string.Empty);
        }

        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT EXISTS (SELECT 1 FROM agent_session WHERE work_item_id = @workItemId AND state IN {AgentSessionStates.LiveSql})",
            new { workItemId }, cancellationToken: cancellationToken));
        return live
            ? new AgentStartPlan(AgentStartBlock.LiveSession, agent.Id, settings.RepoUrl, settings.BaseBranch, prompt)
            : new AgentStartPlan(null, agent.Id, settings.RepoUrl, settings.BaseBranch, prompt);
    }

    // fallbackBaseUrl is the origin of the request, for an instance that has not set its base URL. connectionId and prompt: see PlanAsync;
    // the prompt goes to the agent as guidance and the session stores the connection it ran with.
    // transition is the move that fired an automation, carried in the context; null for a person's Start agent.
    public async Task<AgentStartOutcome> StartAsync(
        Guid workItemId, Guid creatorId, string? fallbackBaseUrl, Guid? connectionId, string? prompt, AgentTransitionInput? transition, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(workItemId, connectionId, prompt, cancellationToken);
        if (plan.Block is { } block)
        {
            return new AgentStartOutcome(null, block);
        }

        var sessionId = Guid.CreateVersion7();
        var now = time.GetUtcNow();
        var input = await _loader.LoadAsync(
            workItemId, sessionId, now, plan.ConnectionId, creatorId, new AgentRepositoryInput(plan.RepoUrl, plan.BaseBranch), fallbackBaseUrl, plan.Prompt, cancellationToken) with { Transition = transition };
        var body = RenderBody(input, workItemId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at)
            VALUES (@sessionId, @workItemId, @connectionId, 'submitted', @repoUrl, @baseBranch, @now, @creatorId, @now)
            ON CONFLICT (work_item_id) WHERE state IN {AgentSessionStates.LiveSql} DO NOTHING
            """,
            new { sessionId, workItemId, connectionId = plan.ConnectionId, repoUrl = plan.RepoUrl, baseBranch = plan.BaseBranch, now, creatorId }, transaction, cancellationToken: cancellationToken));
        if (inserted == 0)
        {
            return new AgentStartOutcome(null, AgentStartBlock.LiveSession);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at)
            VALUES (@id, @sessionId, 1, 'created', @body::jsonb, @now, 'pending', @now)
            """,
            new { id = Guid.CreateVersion7(), sessionId, body, now }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(sessionId);
        return new AgentStartOutcome(sessionId, null);
    }

    // The `created` body as JSON, kept under MaxBodyBytes: first with every attachment as a link only, then without the oldest comments
    // (as many as it takes; the context says how many were left out).
    private string RenderBody(AgentContextInput input, Guid workItemId)
    {
        var body = Serialize(input);
        if (Size(body) <= MaxBodyBytes)
        {
            return body;
        }

        input = input with { Item = input.Item with { Attachments = input.Item.Attachments?.Select(a => a with { Text = null }).ToList() } };
        body = Serialize(input);
        if (Size(body) <= MaxBodyBytes || input.Comments.Count == 0)
        {
            return Log(body, workItemId, 0);
        }

        var ordered = input.Comments.OrderBy(c => c.CreatedAt).ToList();
        AgentContextInput Without(int dropped) => input with
        {
            Comments = ordered.Skip(dropped).ToList(),
            Guidance = $"{input.Guidance}{(string.IsNullOrWhiteSpace(input.Guidance) ? string.Empty : "\n\n")}Note: the {dropped} oldest comment{(dropped == 1 ? " was" : "s were")} left out to keep this message under the size limit.",
        };
        int low = 1, high = ordered.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (Size(Serialize(Without(middle))) <= MaxBodyBytes)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return Log(Serialize(Without(low)), workItemId, low);
    }

    private string Log(string body, Guid workItemId, int droppedComments)
    {
        logger.LogWarning("The agent context of work item {WorkItemId} was over the size limit: attachments sent as links, {Dropped} comments left out, {Bytes} bytes", workItemId, droppedComments, Size(body));
        return body;
    }

    private static string Serialize(AgentContextInput input) => AgentContextRenderer.RenderCreated(input).ToJsonString();

    private static int Size(string body) => System.Text.Encoding.UTF8.GetByteCount(body);
}

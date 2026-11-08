using System.Data.Common;
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
// The blob store is lazy: this starter is built at startup (AgentDispatchSubscriber), and making a folder store then
// would stop the host when its folder is not writable, before any attachment is read.
public sealed class AgentSessionStarter(
    NpgsqlDataSource dataSource, IProjectAgentSettings projectSettings, IAgentConnections connections, AgentSessionNotifier notifier, TimeProvider time, Lazy<IBlobStore> blobs, ILogger<AgentSessionStarter> logger) : IAgentSessionStarter
{
    // agent-relay refuses a body over 1 MiB (413, then retries until the session fails), so the body is kept under this.
    private const int MaxBodyBytes = 900_000;

    private readonly AgentContextLoader _loader = new(dataSource, blobs, logger);

    // The agent check at project level: null when an agent can start for the project, else why not. connectionId, when given,
    // wins over the project's connection, so an override works on a project with none.
    public async Task<AgentStartBlock?> PlanProjectAsync(Guid projectId, Guid? connectionId, CancellationToken cancellationToken)
    {
        var settings = await projectSettings.GetAsync(projectId, cancellationToken);
        var agent = (connectionId ?? settings.ConnectionId) is { } id ? await connections.GetAsync(id, cancellationToken) : null;
        if (agent is null || agent.ArchivedAt is not null)
        {
            return AgentStartBlock.NoConnection;
        }

        return string.IsNullOrWhiteSpace(settings.RepoUrl) ? AgentStartBlock.NoRepository : null;
    }

    public async Task<AgentStartPlan> PlanAsync(Guid workItemId, Guid? connectionId, string? prompt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projectId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That work item was not found.");
        // connectionId overrides the project's connection for every check below; a blank prompt is no prompt.
        prompt = string.IsNullOrWhiteSpace(prompt) ? null : prompt;
        var block = await PlanProjectAsync(projectId, connectionId, cancellationToken);
        var settings = await projectSettings.GetAsync(projectId, cancellationToken);
        if (block is AgentStartBlock.NoConnection)
        {
            return new AgentStartPlan(AgentStartBlock.NoConnection, Guid.Empty, string.Empty, string.Empty);
        }

        var agentId = (connectionId ?? settings.ConnectionId)!.Value;
        if (block is AgentStartBlock.NoRepository)
        {
            return new AgentStartPlan(AgentStartBlock.NoRepository, agentId, string.Empty, string.Empty);
        }

        var repoUrl = settings.RepoUrl!; // PlanProjectAsync found no block, so the repository is set
        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT EXISTS (SELECT 1 FROM agent_session WHERE work_item_id = @workItemId AND state IN {AgentSessionStates.LiveSql})",
            new { workItemId }, cancellationToken: cancellationToken));
        return live
            ? new AgentStartPlan(AgentStartBlock.LiveSession, agentId, repoUrl, settings.BaseBranch, prompt)
            : new AgentStartPlan(null, agentId, repoUrl, settings.BaseBranch, prompt);
    }

    // fallbackBaseUrl is the origin of the request, for an instance that has not set its base URL. connectionId and prompt: see PlanAsync;
    // the prompt goes to the agent as guidance and the session stores the connection it ran with.
    // transition is the move that fired an automation, carried in the context; null for a person's Start agent.
    // mode is stored on the session (implement unless said otherwise); a propose session needs the item's github_issue (Validation
    // otherwise, before anything is written) and gets the allowlisted context (IAgentSessionStarter.StartWinnerAsync).
    public async Task<AgentStartOutcome> StartAsync(
        Guid workItemId, Guid creatorId, string? fallbackBaseUrl, Guid? connectionId, string? prompt, AgentTransitionInput? transition, CancellationToken cancellationToken,
        AgentSessionMode mode = AgentSessionMode.Implement)
    {
        var plan = await PlanAsync(workItemId, connectionId, prompt, cancellationToken);
        if (plan.Block is { } block)
        {
            return new AgentStartOutcome(null, block);
        }

        var sessionId = Guid.CreateVersion7();
        var now = time.GetUtcNow();
        var input = await LoadAsync(workItemId, sessionId, now, plan, creatorId, fallbackBaseUrl, plan.Prompt, mode, null, cancellationToken) with { Transition = transition };
        var body = RenderBody(input, workItemId);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await InsertSessionAsync(connection, transaction, sessionId, workItemId, plan, now, creatorId, mode, cancellationToken))
        {
            return new AgentStartOutcome(null, AgentStartBlock.LiveSession);
        }

        await InsertDeliveryAsync(connection, transaction, sessionId, body, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(sessionId);
        return new AgentStartOutcome(sessionId, null);
    }

    // Like StartAsync, but the caller's prompt is made only by the starter that wins the race, on the winning transaction, so a prompt that
    // issues a token or revokes one (a sprint demo's upload link) never does it for a loser. Order: plan (a block, a live session included, returns
    // with nothing done); load the context with no lock; lock the item row; insert the session, and if the live-session index refuses it roll back and
    // return LiveSession without calling winnerPrompt; call winnerPrompt on the transaction; render the body; insert the delivery; commit.
    // Anything thrown after the insert rolls back the session, the delivery and whatever winnerPrompt wrote.
    // fence, when given, runs on the winning transaction right after the insert (before winnerPrompt): false rolls everything back
    // and returns an outcome with neither a session nor a block.
    public async Task<AgentStartOutcome> StartWinnerAsync(
        Guid workItemId, Guid creatorId, string? fallbackBaseUrl, Guid? connectionId, Func<DbTransaction, CancellationToken, Task<string>> winnerPrompt, CancellationToken cancellationToken,
        AgentSessionMode mode = AgentSessionMode.Implement, IReadOnlyList<AgentRelatedWork>? relatedItems = null, Func<DbTransaction, CancellationToken, Task<bool>>? fence = null)
    {
        if (relatedItems?.Count > IAgentSessionStarter.MaxRelatedItems)
        {
            throw new ArgumentOutOfRangeException(nameof(relatedItems), $"A propose session takes at most {IAgentSessionStarter.MaxRelatedItems} related items.");
        }

        var plan = await PlanAsync(workItemId, connectionId, null, cancellationToken);
        if (plan.Block is { } block)
        {
            return new AgentStartOutcome(null, block);
        }

        var sessionId = Guid.CreateVersion7();
        var now = time.GetUtcNow();
        var input = await LoadAsync(workItemId, sessionId, now, plan, creatorId, fallbackBaseUrl, null, mode, relatedItems, cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM work_item WHERE id = @workItemId FOR NO KEY UPDATE", new { workItemId }, transaction, cancellationToken: cancellationToken));
        if (!await InsertSessionAsync(connection, transaction, sessionId, workItemId, plan, now, creatorId, mode, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AgentStartOutcome(null, AgentStartBlock.LiveSession);
        }

        if (fence is not null && !await fence(transaction, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AgentStartOutcome(null, null);
        }

        var prompt = await winnerPrompt(transaction, cancellationToken);
        var body = RenderBody(input with { Guidance = prompt }, workItemId);
        await InsertDeliveryAsync(connection, transaction, sessionId, body, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(sessionId);
        return new AgentStartOutcome(sessionId, null);
    }

    // A propose session's input is the allowlisted one (it throws Validation when the item has no github_issue, so nothing is written); it
    // never sees plan.RepoUrl, only the base branch.
    private Task<AgentContextInput> LoadAsync(
        Guid workItemId, Guid sessionId, DateTimeOffset now, AgentStartPlan plan, Guid creatorId, string? fallbackBaseUrl, string? prompt, AgentSessionMode mode,
        IReadOnlyList<AgentRelatedWork>? relatedItems, CancellationToken cancellationToken)
    {
        return mode == AgentSessionMode.Propose
            ? _loader.LoadProposeAsync(workItemId, sessionId, now, plan.ConnectionId, creatorId, plan.BaseBranch, prompt, relatedItems ?? [], cancellationToken)
            : _loader.LoadAsync(
                workItemId, sessionId, now, plan.ConnectionId, creatorId, new AgentRepositoryInput(plan.RepoUrl, plan.BaseBranch), fallbackBaseUrl, prompt, cancellationToken);
    }

    // False when the partial unique index ux_agent_session_live finds the item taken.
    private static async Task<bool> InsertSessionAsync(
        NpgsqlConnection connection, DbTransaction transaction, Guid sessionId, Guid workItemId, AgentStartPlan plan, DateTimeOffset now, Guid creatorId, AgentSessionMode mode, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            $"""
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, mode)
            VALUES (@sessionId, @workItemId, @connectionId, 'submitted', @repoUrl, @baseBranch, @now, @creatorId, @now, @mode)
            ON CONFLICT (work_item_id) WHERE state IN {AgentSessionStates.LiveSql} DO NOTHING
            """,
            new { sessionId, workItemId, connectionId = plan.ConnectionId, repoUrl = plan.RepoUrl, baseBranch = plan.BaseBranch, now, creatorId, mode = AgentSessionModes.ToWire(mode) }, transaction, cancellationToken: cancellationToken)) > 0;

    private static Task InsertDeliveryAsync(NpgsqlConnection connection, DbTransaction transaction, Guid sessionId, string body, DateTimeOffset now, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at)
            VALUES (@id, @sessionId, 1, 'created', @body::jsonb, @now, 'pending', @now)
            """,
            new { id = Guid.CreateVersion7(), sessionId, body, now }, transaction, cancellationToken: cancellationToken));

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

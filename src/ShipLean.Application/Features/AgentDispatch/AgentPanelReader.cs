using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.AgentDispatch;

// Reads an item's sessions for the Agent panel: newest first, with their newest 200 activities each, oldest first. Ephemeral activities are
// transient status lines: only the newest activity of the session can be one worth showing, so an ephemeral one that is
// the newest is kept (alone) and every other is dropped. Times are labelled here, never in the browser.
public sealed class AgentPanelReader(NpgsqlDataSource dataSource, TimeProvider time)
{
    // Only the newest this many activities of a session are read; a session with more shows its tail.
    public const int MaxActivities = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AgentSessionDto>> SessionsAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sessions = (await connection.QueryAsync<SessionRow>(new CommandDefinition(
            """
            SELECT s.id, c.name AS connectionName, s.state, s.stop_requested_at AS stopRequestedAt, s.created_at AS startedAt,
                   s.last_activity_at AS lastActivityAt, s.ended_at AS endedAt, s.external_urls::text AS urls, s.plan::text AS plan
            FROM agent_session s JOIN agent_connection c ON c.id = s.connection_id
            WHERE s.work_item_id = @workItemId
            ORDER BY s.created_at DESC, s.id DESC
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var activities = (await connection.QueryAsync<ActivityRow>(new CommandDefinition(
            """
            SELECT x.session_id AS sessionId, x.id, x.type, x.body, x.action, x.parameter, x.result, x.ephemeral, x.signal, x.created_at AS createdAt
            FROM (
                SELECT a.*, row_number() OVER (PARTITION BY a.session_id ORDER BY a.created_at DESC, a.id DESC) AS rn
                FROM agent_activity a JOIN agent_session s ON s.id = a.session_id
                WHERE s.work_item_id = @workItemId AND a.replaced_at IS NULL) x
            WHERE x.rn <= @cap
            ORDER BY x.created_at, x.id
            """,
            new { workItemId, cap = MaxActivities }, cancellationToken: cancellationToken))).ToLookup(a => a.SessionId);

        return sessions.Select(session => ToDto(session, activities[session.Id].ToList())).ToList();
    }

    private AgentSessionDto ToDto(SessionRow session, List<ActivityRow> activities)
    {
        var newest = activities.Count == 0 ? null : activities[^1];
        var shown = activities.Where(a => !a.Ephemeral || a == newest).Select(a => new AgentActivityDto(
            a.Id, a.Type, a.Body, a.Action, a.Parameter, a.Result, a.Ephemeral, a.Signal, a.CreatedAt, DisplayDate.Relative(a.CreatedAt, time))).ToList();
        return new AgentSessionDto(
            session.Id,
            session.ConnectionName,
            session.State,
            session.State is AgentSessionStates.Submitted or AgentSessionStates.Working or AgentSessionStates.InputRequired,
            session.StopRequestedAt is not null,
            session.StartedAt,
            DisplayDate.Relative(session.StartedAt, time),
            session.LastActivityAt,
            DisplayDate.Relative(session.LastActivityAt, time),
            session.EndedAt is { } ended ? DisplayDate.Relative(ended, time) : null,
            JsonSerializer.Deserialize<List<AgentUrlDto>>(session.Urls, Json) ?? [],
            JsonSerializer.Deserialize<List<AgentPlanStepDto>>(session.Plan, Json) ?? [],
            shown);
    }

    private sealed record SessionRow(
        Guid Id, string ConnectionName, string State, DateTimeOffset? StopRequestedAt, DateTimeOffset StartedAt,
        DateTimeOffset LastActivityAt, DateTimeOffset? EndedAt, string Urls, string Plan);

    private sealed record ActivityRow(
        Guid SessionId, Guid Id, string Type, string? Body, string? Action, string? Parameter, string? Result,
        bool Ephemeral, string? Signal, DateTimeOffset CreatedAt);
}

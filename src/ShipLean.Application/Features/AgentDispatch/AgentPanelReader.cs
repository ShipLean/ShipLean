using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.AgentDispatch;

// Reads an item's sessions for the Agent panel: newest first, with their newest 200 activities each, oldest first. Ephemeral activities are
// transient status lines: only the newest activity of the session can be one worth showing, so an ephemeral one that is
// the newest is kept (alone) and every other is dropped. Times are labelled here, never in the browser.
// A session that is not live (terminal or stale) carries its end reason, sentence, detail, run log and delivery attempts, as
// AgentSessionVisibility lets the viewer's project role see them (C3); a live session carries none of them.
public sealed class AgentPanelReader(NpgsqlDataSource dataSource, IAgentDeliveryAttempts attempts, TimeProvider time, IViewerZone viewerZone)
{
    // Only the newest this many activities of a session are read; a session with more shows its tail.
    public const int MaxActivities = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AgentSessionDto>> SessionsAsync(Guid workItemId, ProjectRole viewerRole, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sessions = (await connection.QueryAsync<SessionRow>(new CommandDefinition(
            """
            SELECT s.id, c.name AS connectionName, s.state, s.stop_requested_at AS stopRequestedAt, s.created_at AS startedAt,
                   s.last_activity_at AS lastActivityAt, s.ended_at AS endedAt, s.external_urls::text AS urls, s.plan::text AS plan,
                   s.end_reason AS endReason, s.end_detail AS endDetail, f.id AS runLogId, f.name AS runLogName, f.size_bytes AS runLogSizeBytes
            FROM agent_session s JOIN agent_connection c ON c.id = s.connection_id
            LEFT JOIN attachment f ON f.id = s.run_log_attachment_id AND f.archived_at IS NULL
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

        Guid[] ended = [.. sessions.Where(s => !IsLive(s.State)).Select(s => s.Id)];
        var deliveries = await attempts.ForSessionsAsync(ended, cancellationToken);

        var now = time.GetUtcNow();
        var zone = viewerZone.Zone;
        return sessions.Select(session => ToDto(session, activities[session.Id].ToList(), deliveries[session.Id].ToList(), viewerRole, now, zone)).ToList();
    }

    private static bool IsLive(string state) => state is AgentSessionStates.Submitted or AgentSessionStates.Working or AgentSessionStates.InputRequired;

    private static AgentSessionDto ToDto(
        SessionRow session, List<ActivityRow> activities, List<AgentDeliveryAttemptDto> deliveries, ProjectRole viewerRole, DateTimeOffset now, TimeZoneInfo zone)
    {
        var live = IsLive(session.State);
        var runLog = session.RunLogId is { } logId
            ? new AgentRunLogDto(logId, session.RunLogName!, session.RunLogSizeBytes ?? 0, $"/api/attachments/{logId}/content")
            : null;
        var end = live
            ? new AgentSessionEndView(null, null, null, null, ShowDeliveries: false)
            : AgentSessionVisibility.Apply(session.EndReason, session.EndDetail, runLog, viewerRole);
        var newest = activities.Count == 0 ? null : activities[^1];
        var shown = activities.Where(a => !a.Ephemeral || a == newest).Select(a => new AgentActivityDto(
            a.Id, a.Type, a.Body, a.Action, a.Parameter, a.Result, a.Ephemeral, a.Signal, a.CreatedAt, DisplayDate.Relative(a.CreatedAt, now, zone))).ToList();
        return new AgentSessionDto(
            session.Id,
            session.ConnectionName,
            session.State,
            live,
            session.StopRequestedAt is not null,
            session.StartedAt,
            DisplayDate.Relative(session.StartedAt, now, zone),
            session.LastActivityAt,
            DisplayDate.Relative(session.LastActivityAt, now, zone),
            session.EndedAt is { } ended ? DisplayDate.Relative(ended, now, zone) : null,
            JsonSerializer.Deserialize<List<AgentUrlDto>>(session.Urls, Json) ?? [],
            JsonSerializer.Deserialize<List<AgentPlanStepDto>>(session.Plan, Json) ?? [],
            shown,
            end.EndReason,
            end.EndText,
            end.EndDetail,
            end.RunLog,
            end.ShowDeliveries ? deliveries : []);
    }

    private sealed record SessionRow(
        Guid Id, string ConnectionName, string State, DateTimeOffset? StopRequestedAt, DateTimeOffset StartedAt,
        DateTimeOffset LastActivityAt, DateTimeOffset? EndedAt, string Urls, string Plan, string? EndReason, string? EndDetail,
        Guid? RunLogId, string? RunLogName, long? RunLogSizeBytes);

    private sealed record ActivityRow(
        Guid SessionId, Guid Id, string Type, string? Body, string? Action, string? Parameter, string? Result,
        bool Ephemeral, string? Signal, DateTimeOffset CreatedAt);
}

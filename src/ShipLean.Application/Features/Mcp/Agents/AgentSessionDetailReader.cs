using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Mcp.Agents;

// The reads behind get_agent_session. The item was resolved and the caller's access checked before this is called, so it never
// decides who may see the session: it returns the session, its run log and every delivery attempt, and the tool applies
// AgentSessionVisibility. Activities are paged newest first by a cursor of (created_at, id) that also names its session, so paging by item
// can't slide onto a newer session started between pages (a before from another session is a validation error); every text is capped (AgentTextCap).
public sealed class AgentSessionDetailReader(NpgsqlDataSource dataSource, IAgentDeliveryAttempts attempts)
{
    // The session (it belongs to the item) or the item's newest; null when there is none.
    public async Task<AgentSessionDetail?> ReadAsync(Guid itemId, string itemKey, Guid? session, string? before, int limit, CancellationToken cancellationToken)
    {
        var cursor = ParseCursor(before);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            """
            SELECT s.id, c.name AS connection, s.state, s.created_at AS started, s.ended_at AS ended, s.end_reason AS endReason, s.end_detail AS endDetail,
                   f.id AS runLogId, f.name AS runLogName, f.size_bytes AS runLogSizeBytes
            FROM agent_session s
            JOIN agent_connection c ON c.id = s.connection_id
            LEFT JOIN attachment f ON f.id = s.run_log_attachment_id AND f.archived_at IS NULL
            WHERE s.work_item_id = @itemId AND (@session::uuid IS NULL OR s.id = @session)
            ORDER BY s.created_at DESC, s.id DESC
            LIMIT 1
            """,
            new { itemId, session }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        if (cursor is not null && cursor.Value.SessionId != row.Id)
        {
            throw ApiException.Validation(
                "before is from a different agent session than the one this call reads (the item's newest session may have changed). Pass session with the id of the session you were paging.",
                "before");
        }

        var page = (await connection.QueryAsync<ActivityRow>(new CommandDefinition(
            """
            SELECT a.id, a.type, a.body, a.action, a.parameter, a.result, a.created_at AS at
            FROM agent_activity a
            WHERE a.session_id = @id AND a.replaced_at IS NULL
              AND (@hasCursor = false OR (a.created_at, a.id) < (@cursorAt, @cursorId))
            ORDER BY a.created_at DESC, a.id DESC
            LIMIT @take
            """,
            new { row.Id, hasCursor = cursor is not null, cursorAt = cursor?.At ?? DateTimeOffset.MinValue, cursorId = cursor?.Id ?? Guid.Empty, take = limit + 1 },
            cancellationToken: cancellationToken))).ToList();
        var more = page.Count > limit;
        var shown = page.Take(limit).ToList();

        var deliveries = (await attempts.ForSessionsAsync([row.Id], cancellationToken))[row.Id].ToList();
        var runLog = row.RunLogId is { } logId
            ? new AgentRunLogDto(logId, row.RunLogName!, row.RunLogSizeBytes ?? 0, $"/api/attachments/{logId}/content")
            : null;
        return new AgentSessionDetail(
            new AgentSessionRow(row.Id, row.Connection, row.State, row.Started, row.Ended, row.EndReason, row.EndDetail),
            itemKey,
            runLog,
            [.. shown.Select(ToActivity)],
            more ? CursorOf(row.Id, shown[^1]) : null,
            deliveries);
    }

    private static AgentSessionDetail.Activity ToActivity(ActivityRow a)
    {
        var truncated = false;
        var body = AgentTextCap.Cap(a.Body, ref truncated);
        var action = AgentTextCap.Cap(a.Action, ref truncated);
        var parameter = AgentTextCap.Cap(a.Parameter, ref truncated);
        var result = AgentTextCap.Cap(a.Result, ref truncated);
        return new AgentSessionDetail.Activity(a.Id, a.Type, body, action, parameter, result, truncated, a.At);
    }

    private static string CursorOf(Guid sessionId, ActivityRow last) => $"{sessionId:N}.{last.At.UtcTicks.ToString(CultureInfo.InvariantCulture)}.{last.Id:N}";

    private static (Guid SessionId, DateTimeOffset At, Guid Id)? ParseCursor(string? before)
    {
        if (string.IsNullOrWhiteSpace(before))
        {
            return null;
        }

        var parts = before.Split('.');
        if (parts.Length == 3
            && Guid.TryParseExact(parts[0], "N", out var sessionId)
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            && ticks <= DateTimeOffset.MaxValue.Ticks
            && Guid.TryParseExact(parts[2], "N", out var id))
        {
            return (sessionId, new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }

        throw ApiException.Validation("before must be the next_before of a previous get_agent_session call.", "before");
    }

    private sealed record SessionRow(
        Guid Id, string Connection, string State, DateTimeOffset Started, DateTimeOffset? Ended, string? EndReason, string? EndDetail,
        Guid? RunLogId, string? RunLogName, long? RunLogSizeBytes);

    private sealed record ActivityRow(Guid Id, string Type, string? Body, string? Action, string? Parameter, string? Result, DateTimeOffset At);
}

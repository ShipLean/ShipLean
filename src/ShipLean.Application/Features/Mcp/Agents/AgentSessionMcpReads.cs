using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Mcp.Agents;

// The SQL behind get_work_item's agent_sessions and get_agent_session's access check. Access is the caller's job: the item id
// passed in was resolved through the MCP item resolver first.
public sealed class AgentSessionMcpReads(NpgsqlDataSource dataSource)
{
    // The newest sessions of an item, newest first.
    public async Task<IReadOnlyList<AgentSessionRow>> NewestAsync(Guid itemId, int count, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AgentSessionRow>(new CommandDefinition(
            """
            SELECT s.id, c.name AS connection, s.state, s.created_at AS started, s.ended_at AS ended, s.end_reason AS endReason, s.end_detail AS endDetail
            FROM agent_session s
            JOIN agent_connection c ON c.id = s.connection_id
            WHERE s.work_item_id = @itemId
            ORDER BY s.created_at DESC, s.id DESC
            LIMIT @count
            """,
            new { itemId, count }, cancellationToken: cancellationToken));
        return [.. rows];
    }

    // The key of the item a session belongs to; null when there is no such session.
    public async Task<string?> ItemKeyOfAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT w.key FROM agent_session s JOIN work_item w ON w.id = s.work_item_id WHERE s.id = @sessionId",
            new { sessionId }, cancellationToken: cancellationToken));
    }
}

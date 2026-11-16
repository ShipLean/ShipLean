using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.RunLogs;

// IRunLogAttachments by SQL over agent_session.run_log_attachment_id. An admin is never asked the database.
public sealed class RunLogAttachments(NpgsqlDataSource dataSource) : IRunLogAttachments
{
    public async Task<IReadOnlySet<Guid>> HiddenAsync(Guid workItemId, ProjectRole role, CancellationToken cancellationToken = default)
    {
        if (role == ProjectRole.Admin)
        {
            return new HashSet<Guid>();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT run_log_attachment_id FROM agent_session WHERE work_item_id = @workItemId AND run_log_attachment_id IS NOT NULL",
            new { workItemId }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task<bool> IsHiddenAsync(Guid attachmentId, ProjectRole role, CancellationToken cancellationToken = default)
    {
        if (role == ProjectRole.Admin)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM agent_session WHERE run_log_attachment_id = @attachmentId)",
            new { attachmentId }, cancellationToken: cancellationToken));
    }
}

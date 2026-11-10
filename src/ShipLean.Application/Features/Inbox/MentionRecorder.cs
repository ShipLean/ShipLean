using System.Data.Common;
using Dapper;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.Inbox;

// IMentionRecorder over comment_mention, on the comment's own transaction. The candidates are the live members of the item's
// project by full name. A person's comment never mentions its author; an agent's comment may name the person it runs for. A
// comment mirrored from GitHub mentions no one. Rows are never deleted: an edit withdraws open mentions whose name was removed.
public sealed class MentionRecorder(TimeProvider clock) : IMentionRecorder
{
    public async Task RecordAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string body, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default)
    {
        var mentioned = await FindAsync(transaction, workItemId, body, authorUserId, agentName, cancellationToken);
        await InsertAsync(transaction, commentId, workItemId, mentioned, cancellationToken);
    }

    // New names get an open mention (one already recorded, in any state, is left as it is); open mentions whose name is gone
    // are withdrawn. Seen and replied mentions stay.
    public async Task ReconcileAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string newBody, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default)
    {
        var mentioned = await FindAsync(transaction, workItemId, newBody, authorUserId, agentName, cancellationToken);
        await InsertAsync(transaction, commentId, workItemId, mentioned, cancellationToken);
        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE comment_mention SET state = 'withdrawn', closed_at = @now
            WHERE comment_id = @commentId AND state = 'open' AND NOT (user_id = ANY(@mentioned))
            """,
            new { commentId, now = clock.GetUtcNow(), mentioned = mentioned.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<Guid>> FindAsync(
        DbTransaction transaction, Guid workItemId, string body, Guid? authorUserId, string? agentName, CancellationToken cancellationToken)
    {
        if (agentName is not null && agentName.StartsWith(MirroredComment.AgentNamePrefix, StringComparison.Ordinal))
        {
            return [];
        }

        var members = await transaction.Connection!.QueryAsync<MentionCandidate>(new CommandDefinition(
            """
            SELECT u.id AS UserId, u.name AS Name
            FROM work_item w
            JOIN project_member m ON m.project_id = w.project_id AND m.archived_at IS NULL
            JOIN app_user u ON u.id = m.user_id
            WHERE w.id = @workItemId
            """,
            new { workItemId },
            transaction,
            cancellationToken: cancellationToken));
        return MentionText.Find(body, [.. members], agentName is null ? authorUserId : null);
    }

    private async Task InsertAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, IReadOnlyList<Guid> mentioned, CancellationToken cancellationToken)
    {
        if (mentioned.Count == 0)
        {
            return;
        }

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO comment_mention (id, comment_id, work_item_id, user_id, state, created_at)
            SELECT m.id, @commentId, @workItemId, m.user_id, 'open', @now
            FROM unnest(@ids, @userIds) AS m (id, user_id)
            ON CONFLICT (comment_id, user_id) DO NOTHING
            """,
            new
            {
                commentId, workItemId, now = clock.GetUtcNow(),
                ids = mentioned.Select(_ => Guid.NewGuid()).ToArray(), userIds = mentioned.ToArray(),
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}

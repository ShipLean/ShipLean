using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.TaskChanges.Review;

// The SQL behind Request changes: one UPDATE marks the open top-level comments as sent back and says how many there were.
internal static class SentBackRows
{
    public static async Task<int> MarkAsync(NpgsqlConnection connection, Guid workItemId, DateTimeOffset at, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE code_comment SET sent_back_at = @at
            WHERE work_item_id = @workItemId AND parent_id IS NULL AND archived_at IS NULL AND resolved_at IS NULL
            """,
            new { workItemId, at }, cancellationToken: cancellationToken));

    public static async Task<int> CountOpenAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
        => await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM code_comment
            WHERE work_item_id = @workItemId AND parent_id IS NULL AND archived_at IS NULL AND resolved_at IS NULL
            """,
            new { workItemId }, cancellationToken: cancellationToken));
}

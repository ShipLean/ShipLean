using System.Text.Json;
using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Inbox;

// Reads the Inbox's stored rows back raw, for tests that assert what a writer stored: comment_mention rows (state as the stored
// string, times UTC) and a question's options in their stored order.
public static class InboxRows
{
    public sealed record Mention(
        Guid Id, Guid CommentId, Guid WorkItemId, Guid UserId, string State, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt,
        Guid? ReplyCommentId);

    // The mentions recorded for a comment, in user id order.
    public static async Task<IReadOnlyList<Mention>> MentionsAsync(NpgsqlConnection connection, Guid commentId) =>
        (await connection.QueryAsync<Mention>(
            """
            SELECT id AS Id, comment_id AS CommentId, work_item_id AS WorkItemId, user_id AS UserId, state AS State, created_at AS CreatedAt,
                   closed_at AS ClosedAt, reply_comment_id AS ReplyCommentId
            FROM comment_mention WHERE comment_id = @commentId ORDER BY user_id
            """,
            new { commentId })).ToList();

    public static async Task<IReadOnlyList<string>> QuestionOptionsAsync(NpgsqlConnection connection, Guid questionId)
    {
        var json = await connection.ExecuteScalarAsync<string>("SELECT options::text FROM question WHERE id = @questionId", new { questionId });
        return JsonSerializer.Deserialize<string[]>(json!)!;
    }
}

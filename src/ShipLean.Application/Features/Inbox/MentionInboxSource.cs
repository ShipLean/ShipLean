using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

// The user's open @mentions: comment_mention rows for the user still open, on a comment that isn't archived, on an item not in
// the Removed category, in a project the user is still a live member of. From is the comment's author and AgentName its agent
// stamp; At is when the mention was made. Labels stay "@": the reader numbers them per item. Ordering is the reader's.
public sealed partial class MentionInboxSource(NpgsqlDataSource dataSource, TimeProvider clock) : IInboxAskSource
{
    private const string Ellipsis = "…";

    private const string OpenMentions =
        """
        m.user_id = @userId AND m.state = 'open' AND c.archived_at IS NULL
        AND EXISTS (SELECT 1 FROM state item_state WHERE item_state.id = wi.state_id AND item_state.category <> 'removed')
        AND EXISTS (SELECT 1 FROM project_member member
                    WHERE member.project_id = wi.project_id AND member.user_id = @userId AND member.archived_at IS NULL)
        """;

    private const string MentionJoins =
        """
        FROM comment_mention m
        JOIN comment c ON c.id = m.comment_id
        JOIN work_item wi ON wi.id = m.work_item_id
        """;

    public async Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var mentions = (await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
            SELECT m.id AS mentionId, m.comment_id AS commentId, m.work_item_id AS workItemId, m.created_at AS createdAt,
                   c.body, c.author_id AS authorId, c.agent_name AS agentName
            {MentionJoins}
            WHERE wi.project_id = @projectId AND {OpenMentions}
            """,
            new { userId, projectId }, cancellationToken: cancellationToken))).ToList();
        var authorIds = mentions.Select(m => m.AuthorId).OfType<Guid>().Distinct().ToArray();
        var authors = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@authorIds)",
            new { authorIds }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);

        return [.. mentions.Select(m => new InboxAskRow(m.WorkItemId, new InboxAskDto(
            InboxAskKind.Mention, "@", m.CreatedAt, DisplayDate.Relative(m.CreatedAt, clock),
            m.AuthorId is { } author ? authors[author] : null, m.AgentName, null, null,
            new InboxMentionDto(m.MentionId, m.CommentId, Excerpt(m.Body)), null)))];
    }

    public async Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var counts = await connection.QueryAsync<(Guid ProjectId, int Count)>(new CommandDefinition(
            $"SELECT wi.project_id, count(*)::int {MentionJoins} WHERE {OpenMentions} GROUP BY wi.project_id",
            new { userId }, cancellationToken: cancellationToken));
        return counts.ToDictionary(c => c.ProjectId, c => c.Count);
    }

    // Whether the user was ever mentioned, in any state.
    public async Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM comment_mention WHERE user_id = @userId)",
            new { userId }, cancellationToken: cancellationToken));
    }

    // The comment as plain text on one line, at most InboxRules.ExcerptLength characters: a longer one is cut at the last word
    // that fits with "…".
    private static string Excerpt(string markdown)
    {
        var plain = Whitespace().Replace(Markdig.Markdown.ToPlainText(markdown), " ").Trim();
        if (plain.Length <= InboxRules.ExcerptLength)
        {
            return plain;
        }

        var room = InboxRules.ExcerptLength - Ellipsis.Length;
        var space = plain.LastIndexOf(' ', room);
        var cut = space > 0 ? space : room;
        if (char.IsHighSurrogate(plain[cut - 1]))
        {
            cut--;
        }

        return plain[..cut].TrimEnd() + Ellipsis;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private sealed record Row(Guid MentionId, Guid CommentId, Guid WorkItemId, DateTimeOffset CreatedAt, string Body, Guid? AuthorId, string? AgentName);
}

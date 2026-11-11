using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

// What the current user answered in one project in the last InboxRules.AnsweredWindow, newest first, at most
// InboxRules.AnsweredLimit rows, and what happened next on each item.
//
// Answers are the user's own confirmed, changed and answered ask_event rows (events on one item with the same kind and instant
// make one row: "Approved D1–D8"), and their mentions closed as seen or replied. Discuss and "still true" are not answers and
// leave no row. Next is the earliest event after the answer on the same item that an agent or another person made: a live
// comment or a non-bulk revision with an agent name is agentPickedUp, a live comment by another person without one is
// personReplied. Archived comments, comments mirrored from GitHub, the comments an answer posts itself, bulk revisions and the
// user's own plain comments don't count. One query finds every row's next event.
public sealed class AnsweredReader(NpgsqlDataSource dataSource, ICurrentUser currentUser, TimeProvider clock) : IAnsweredReader
{
    public async Task<AnsweredDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;
        var now = clock.GetUtcNow();
        var since = now - InboxRules.AnsweredWindow;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var events = (await connection.QueryAsync<EventRow>(new CommandDefinition(
            """
            SELECT e.work_item_id AS workItemId, e.kind, e.at, d.number AS decisionNumber, q.number AS questionNumber
            FROM ask_event e
            JOIN work_item wi ON wi.id = e.work_item_id
            LEFT JOIN decision d ON d.id = e.decision_id
            LEFT JOIN question q ON q.id = e.question_id
            WHERE wi.project_id = @projectId AND e.by_user_id = @userId AND e.kind IN ('confirmed', 'changed', 'answered')
              AND e.at >= @since
            """,
            new { projectId, userId, since }, cancellationToken: cancellationToken))).ToList();
        var mentions = (await connection.QueryAsync<MentionRow>(new CommandDefinition(
            """
            SELECT m.work_item_id AS workItemId, m.state, m.closed_at AS closedAt, author.name AS authorName
            FROM comment_mention m
            JOIN work_item wi ON wi.id = m.work_item_id
            JOIN comment c ON c.id = m.comment_id
            LEFT JOIN app_user author ON author.id = c.author_id
            WHERE wi.project_id = @projectId AND m.user_id = @userId AND m.state IN ('seen', 'replied') AND m.closed_at >= @since
            """,
            new { projectId, userId, since }, cancellationToken: cancellationToken))).ToList();

        var answers = events
            .GroupBy(e => (e.WorkItemId, e.Kind, e.At))
            .Select(group => Summarise(group.Key.WorkItemId, group.Key.Kind, group.Key.At, group.ToList()))
            .Concat(mentions.Select(m => new Answer(
                m.WorkItemId, InboxAskKind.Mention, "@", MentionSummary(m), m.ClosedAt)))
            .ToList();
        if (answers.Count == 0)
        {
            return new AnsweredDto(0, 0, []);
        }

        var itemIds = answers.Select(a => a.WorkItemId).Distinct().ToArray();
        var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition(
            "SELECT id, key, type, title FROM work_item WHERE id = ANY(@itemIds)",
            new { itemIds }, cancellationToken: cancellationToken))).ToDictionary(i => i.Id);
        var nexts = await NextsAsync(connection, answers, userId, cancellationToken);

        var today = DisplayDate.StartOfDay(DisplayDate.Today(now, clock.LocalTimeZone), clock.LocalTimeZone);
        var rows = answers
            .Select((a, index) => new AnsweredRowDto(
                a.WorkItemId, items[a.WorkItemId].Key, items[a.WorkItemId].Type, items[a.WorkItemId].Title, a.Kind, a.Labels, a.Summary,
                a.At, DisplayDate.Relative(a.At, clock), nexts.GetValueOrDefault(index)))
            .OrderByDescending(r => r.AnsweredAt).ThenBy(r => r.Key, StringComparer.Ordinal).ThenBy(r => r.Labels, StringComparer.Ordinal)
            .ToList();
        var todays = rows.Where(r => r.AnsweredAt >= today).ToList();
        return new AnsweredDto(
            todays.Count, todays.Count(r => r.Next?.Kind == AnsweredNextKind.AgentPickedUp), [.. rows.Take(InboxRules.AnsweredLimit)]);
    }

    private static Answer Summarise(Guid workItemId, string kind, DateTimeOffset at, List<EventRow> events)
    {
        if (kind == "answered")
        {
            var labels = Range('Q', events.Select(e => e.QuestionNumber));
            return new Answer(workItemId, InboxAskKind.Question, labels, $"Answered {labels}", at);
        }

        var decisions = Range('D', events.Select(e => e.DecisionNumber));
        return new Answer(workItemId, InboxAskKind.Decision, decisions, $"{(kind == "confirmed" ? "Approved" : "Changed")} {decisions}", at);
    }

    private static string MentionSummary(MentionRow mention)
    {
        if (mention.State != "replied")
        {
            return "Marked seen";
        }

        return mention.AuthorName is { } name ? $"Replied to {name}" : "Replied";
    }

    // "D1–D3, D5": the numbers in order, a run of two or more joined by an en dash.
    private static string Range(char prefix, IEnumerable<int?> numbers)
    {
        var sorted = numbers.OfType<int>().Distinct().Order().ToList();
        var parts = new List<string>();
        for (var i = 0; i < sorted.Count;)
        {
            var end = i;
            while (end + 1 < sorted.Count && sorted[end + 1] == sorted[end] + 1)
            {
                end++;
            }

            parts.Add(end == i ? $"{prefix}{sorted[i]}" : $"{prefix}{sorted[i]}–{prefix}{sorted[end]}");
            i = end + 1;
        }

        return string.Join(", ", parts);
    }

    // The next event for every answer, by its index in answers: one lateral join over the answers' (item, time) pairs.
    private async Task<Dictionary<int, AnsweredNextDto>> NextsAsync(
        NpgsqlConnection connection, List<Answer> answers, Guid userId, CancellationToken cancellationToken)
    {
        var found = await connection.QueryAsync<NextRow>(new CommandDefinition(
            """
            SELECT t.idx - 1 AS idx, n.kind, n.by_user_id AS byUserId, n.agent_name AS agentName, n.at,
                   u.id, u.name, u.initials, u.avatar_colour AS avatarColour, {PhotoUrl} AS photoUrl
            FROM unnest(@itemIds::uuid[], @ats::timestamptz[]) WITH ORDINALITY AS t(item_id, answered_at, idx)
            CROSS JOIN LATERAL (
                SELECT e.kind, e.by_user_id, e.agent_name, e.at
                FROM (
                    SELECT CASE WHEN c.agent_name IS NOT NULL THEN 'agentPickedUp' ELSE 'personReplied' END AS kind,
                           c.author_id AS by_user_id, c.agent_name, c.created_at AS at
                    FROM comment c
                    WHERE c.work_item_id = t.item_id AND c.created_at > t.answered_at AND c.archived_at IS NULL
                      AND NOT EXISTS (SELECT 1 FROM github_issue_comment g WHERE g.comment_id = c.id)
                      AND NOT (c.author_id = @userId AND EXISTS (SELECT 1 FROM comment_ask_tag tag WHERE tag.comment_id = c.id AND tag.kind IN ('answer', 'changed')))
                      AND (c.agent_name IS NOT NULL OR c.author_id IS DISTINCT FROM @userId AND c.author_id IS NOT NULL)
                    UNION ALL
                    SELECT 'agentPickedUp', r.changed_by, r.agent_name, r.changed_at
                    FROM revision r
                    WHERE r.work_item_id = t.item_id AND r.changed_at > t.answered_at AND r.agent_name IS NOT NULL AND NOT r.bulk
                ) e
                ORDER BY e.at, e.kind
                LIMIT 1
            ) n
            LEFT JOIN app_user u ON u.id = n.by_user_id
            """.Replace("{PhotoUrl}", UserRefs.PhotoUrlSql),
            new { itemIds = answers.Select(a => a.WorkItemId).ToArray(), ats = answers.Select(a => a.At.ToUniversalTime()).ToArray(), userId },
            cancellationToken: cancellationToken));
        return found.ToDictionary(
            n => (int)n.Idx,
            n => new AnsweredNextDto(
                n.Kind == "agentPickedUp" ? AnsweredNextKind.AgentPickedUp : AnsweredNextKind.PersonReplied,
                n.ByUserId is null ? null : new UserRef(n.Id!.Value, n.Name!, n.Initials!, n.AvatarColour!, n.PhotoUrl),
                n.AgentName, n.At, DisplayDate.Relative(n.At, clock)));
    }

    private sealed record Answer(Guid WorkItemId, InboxAskKind Kind, string Labels, string Summary, DateTimeOffset At);

    private sealed record EventRow(Guid WorkItemId, string Kind, DateTimeOffset At, int? DecisionNumber, int? QuestionNumber);

    private sealed record MentionRow(Guid WorkItemId, string State, DateTimeOffset ClosedAt, string? AuthorName);

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title);

    private sealed record NextRow(
        long Idx, string Kind, Guid? ByUserId, string? AgentName, DateTimeOffset At,
        Guid? Id, string? Name, string? Initials, string? AvatarColour, string? PhotoUrl);
}

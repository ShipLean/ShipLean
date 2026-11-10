using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.Core;

namespace ShipLean.Application.Features.FeaturesOverview;

// What changed in a project since the caller last pressed Mark as seen (feature_digest_seen, 24 hours back when they never have),
// oldest change first, a page per cursor. The first page leads with NeedsYou: the open decisions and questions raised since then
// on items the caller owns (AskOwnerSql). Later pages carry Changed only. Callers check Member access first.
public sealed class FeatureDigest(NpgsqlDataSource dataSource, IWorkItemChanges changes, ICurrentUser currentUser, TimeProvider clock) : IFeatureDigest
{
    private const int PageSize = 100;

    public async Task<FeatureDigestDto> GetAsync(Guid projectId, string? cursor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var seenAt = await connection.ExecuteScalarAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT seen_at FROM feature_digest_seen WHERE user_id = @userId AND project_id = @projectId",
            new { userId = currentUser.Id, projectId }, cancellationToken: cancellationToken));
        var (since, sinceLabel) = FeaturesSince.Of(seenAt, clock);

        var page = cursor is null
            ? await changes.SinceTimeAsync(projectId, since, PageSize, null, cancellationToken)
            : await changes.SinceAsync(projectId, cursor, PageSize, null, cancellationToken);
        var users = await UsersAsync(connection, page.Entries.Select(e => e.ActorId).OfType<Guid>(), cancellationToken);
        var changed = page.Entries
            .Select(entry => (Entry: entry, Lines: DigestLines.Of(entry, entry.ActorId is { } actor ? users.GetValueOrDefault(actor) : null, clock)))
            .Where(pair => pair.Lines.Count > 0)
            .GroupBy(pair => pair.Entry.WorkItemId)
            .Select(group => new DigestItemDto(group.Key, group.First().Entry.Key, group.First().Entry.Type!, group.First().Entry.Title, [.. group.SelectMany(pair => pair.Lines)]))
            .ToList();

        var needsYou = cursor is null ? await NeedsYouAsync(connection, projectId, since, cancellationToken) : [];
        return new FeatureDigestDto(since, sinceLabel, needsYou, changed, page.Cursor, page.More);
    }

    // Moves the caller's seen time to now, replacing any earlier row (one row per person and project).
    public async Task MarkSeenAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO feature_digest_seen (user_id, project_id, seen_at) VALUES (@userId, @projectId, @now)
            ON CONFLICT (user_id, project_id) DO UPDATE SET seen_at = excluded.seen_at
            """,
            new { userId = currentUser.Id, projectId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<DigestItemDto>> NeedsYouAsync(NpgsqlConnection connection, Guid projectId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<AskRow>(new CommandDefinition(
            $"""
            SELECT wi.id AS workItemId, wi.key, wi.type, wi.title, true AS isDecision, d.question AS text,
                   d.created_by_user_id AS raisedBy, d.created_agent_name AS agentName, d.created_at AS at
            FROM decision d JOIN work_item wi ON wi.id = d.work_item_id
            WHERE d.state = 'open' AND d.created_at >= @since AND wi.project_id = @projectId AND {AskOwnerSql.OwnedByUser}
            UNION ALL
            SELECT wi.id, wi.key, wi.type, wi.title, false, q.text, q.created_by_user_id, q.created_agent_name, q.created_at
            FROM question q JOIN work_item wi ON wi.id = q.work_item_id
            WHERE q.state = 'open' AND q.created_at >= @since AND wi.project_id = @projectId AND {AskOwnerSql.OwnedByUser}
            ORDER BY at, key
            """,
            new { userId = currentUser.Id, projectId, since }, cancellationToken: cancellationToken))).ToList();
        var users = await UsersAsync(connection, rows.Select(r => r.RaisedBy), cancellationToken);
        return rows
            .GroupBy(r => r.WorkItemId)
            .Select(group => new DigestItemDto(
                group.Key, group.First().Key, group.First().Type, group.First().Title,
                [.. group.Select(r => new DigestLineDto(
                    DigestLineKind.Ask, $"{(r.IsDecision ? "Decision" : "Needs input")}: {r.Text}", users.GetValueOrDefault(r.RaisedBy), r.AgentName, r.At, DisplayDate.Relative(r.At, clock)))]))
            .ToList();
    }

    private static async Task<IReadOnlyDictionary<Guid, UserRef>> UsersAsync(NpgsqlConnection connection, IEnumerable<Guid> ids, CancellationToken cancellationToken) =>
        (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)",
            new { ids = ids.Distinct().ToArray() }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);

    private sealed record AskRow(Guid WorkItemId, string Key, string Type, string Title, bool IsDecision, string Text, Guid RaisedBy, string? AgentName, DateTimeOffset At);
}

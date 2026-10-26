using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking.Fusion;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking;

// Exact key first, then RRF over the legs' lists, then the boosts (Fusion/SearchBoosts). Semantic is true only when the
// embedder is semantic, every semantic leg is available and none of them has rows still waiting for an embedding.
// The exact key ignores IncludeClosed (a quick menu typed "SHIP-12" finds a done item), but when Types, Tags or Criteria
// are set it only promotes an item a leg already listed, so the filters still hold.
public sealed partial class SearchRanker(
    IEnumerable<IItemCandidates> legs,
    ISearchGroups groups,
    IEmbedder embedder,
    SearchOptions options,
    ICurrentUser currentUser,
    TimeProvider clock,
    NpgsqlDataSource dataSource,
    ILogger<SearchRanker> logger) : ISearchRanker
{
    public async Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Criteria is not null && query.ProjectIds is not { Count: 1 })
        {
            throw ApiException.Validation("Criteria need exactly one project.", "project");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var memberships = (await connection.QueryAsync<Membership>(new CommandDefinition(
            """
            SELECT p.id, p.key, up.json::text AS recent
            FROM project_member m
            JOIN project p ON p.id = m.project_id
            LEFT JOIN user_preference up ON up.user_id = m.user_id AND up.key = 'recentProjects'
            WHERE m.user_id = @me AND m.archived_at IS NULL
            ORDER BY p.key
            """,
            new { me = currentUser.Id }, cancellationToken: cancellationToken))).ToList();
        var projectIds = query.ProjectIds ?? [.. memberships.Select(m => m.Id)];
        var embedderSemantic = embedder.Status.Mode == EmbedderMode.Semantic;
        var candidateQuery = new CandidateQuery(
            query.Text,
            projectIds,
            query.Types,
            query.Tags,
            query.IncludeClosed,
            query.Prefix,
            query.Criteria,
            embedderSemantic ? await embedder.EmbedQueryAsync(query.Text, cancellationToken) : null);

        var lists = new List<RankedList>();
        var semanticLegs = new List<CandidateLists>();
        foreach (var leg in legs)
        {
            var result = await leg.ListsAsync(candidateQuery, cancellationToken);
            lists.AddRange(result.Lists);
            if (leg.Leg == CandidateLeg.Semantic)
            {
                semanticLegs.Add(result);
            }
        }

        var semantic = IsSemantic(embedderSemantic, semanticLegs);
        var fused = ReciprocalRankFusion.Fuse(lists, options.RrfK).ToDictionary(f => f.Key, f => f.Value);
        var exactId = ExactKey.TryParse(query.Text, out var key) ? await ExactItemAsync(connection, key, projectIds, cancellationToken) : null;
        var facts = fused.Count == 0 ? new Dictionary<Guid, ItemFacts>() : await FactsAsync(connection, [.. fused.Keys], projectIds, cancellationToken);

        var now = clock.GetUtcNow();
        var recentProject = RecentProject(memberships);
        var ranked = new List<(FusedItem Item, Guid Id)>();
        foreach (var (id, item) in fused)
        {
            // The legs already leave these out; the ranker doesn't trust a double or a stale row to.
            // A fused id with no row means a leg returned something outside the projects: a leg bug, so it surfaces.
            var fact = facts.GetValueOrDefault(id) ?? throw new InvalidOperationException($"A search leg returned work item {id}, which is not in the searched projects.");
            if (fact.Category == StateCategory.Removed || (!query.IncludeClosed && fact.Category == StateCategory.Completed))
            {
                continue;
            }

            item.Score *= SearchBoosts.Factor(fact, currentUser.Id, recentProject, now);
            ranked.Add((item, id));
        }

        // Types, tags and criteria describe items, so projects and sprints are not looked up for them.
        var itemOnly = query.Types is { Count: > 0 } || query.Tags is { Count: > 0 } || query.Criteria is not null;
        // With item filters the legs applied them, so only an item they listed (and that survived above) may be promoted.
        if (exactId is { } exact && (!itemOnly || ranked.Exists(r => r.Id == exact)))
        {
            // A leg's own hit keeps its field and snippet. Otherwise it is a title hit with no snippet, which ToItem shows as the title.
            var best = fused.TryGetValue(exact, out var listed) ? listed.Best : new CandidateHit(exact, SearchField.Title, 1, "");
            ranked.RemoveAll(r => r.Id == exact);
            ranked.Add((new FusedItem(best, 0) { Score = SearchBoosts.ExactKeyScore }, exact));
        }

        var ordered = ranked.OrderByDescending(r => r.Item.Score).Select(r => r.Id).ToList();
        var page = ordered.Take(query.Limit).ToArray();
        var byId = ranked.ToDictionary(r => r.Id, r => r.Item);
        var details = await DetailsAsync(connection, page, cancellationToken);

        // Each group call opens its own connection, so the two run side by side.
        var projectsTask = itemOnly ? Task.FromResult<IReadOnlyList<SearchResultProject>>([]) : groups.ProjectsAsync(candidateQuery, cancellationToken);
        var sprintsTask = itemOnly ? Task.FromResult<IReadOnlyList<SearchResultSprint>>([]) : groups.SprintsAsync(candidateQuery, cancellationToken);
        await Task.WhenAll(projectsTask, sprintsTask);
        return new SearchResults(
            semantic,
            [.. (await projectsTask).Take(GroupLimits.MaxProjects)],
            [.. (await sprintsTask).Take(GroupLimits.MaxSprints)],
            [.. page.Select(id => details[id].ToItem(byId[id]))],
            ordered.Count > query.Limit);
    }

    // Logs the reason once per query when the answer is lexical only.
    private bool IsSemantic(bool embedderSemantic, List<CandidateLists> semanticLegs)
    {
        if (!embedderSemantic)
        {
            LogLexicalOnly(embedder.Status.Reason ?? "embeddings are off");
            return false;
        }

        if (semanticLegs.Count == 0 || semanticLegs.Exists(l => !l.Available))
        {
            LogSemanticUnavailable();
            return false;
        }

        var pending = semanticLegs.Sum(l => l.PendingRows);
        if (pending > 0)
        {
            LogRowsPending(pending);
            return false;
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search is lexical only: {Reason}")]
    private partial void LogLexicalOnly(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search is lexical only: the embedder is semantic but no semantic leg answered")]
    private partial void LogSemanticUnavailable();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Search is lexical only: {PendingRows} matching rows are not embedded yet")]
    private partial void LogRowsPending(int pendingRows);

    // The item the key names, if it is in an allowed project the caller is in and not removed.
    private async Task<Guid?> ExactItemAsync(NpgsqlConnection connection, string key, IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT w.id FROM work_item w
            JOIN state s ON s.id = w.state_id
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE w.key = @key AND w.project_id = ANY(@projectIds) AND s.category <> 'removed'
            """,
            new { key, projectIds = projectIds.ToArray(), me = currentUser.Id }, cancellationToken: cancellationToken));

    private static async Task<Dictionary<Guid, ItemFacts>> FactsAsync(NpgsqlConnection connection, Guid[] ids, IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<FactsRow>(new CommandDefinition(
            """
            SELECT w.id, w.project_id AS projectId, s.category AS categoryText, w.assigned_to AS assigneeId, w.changed_at AS changedAt
            FROM work_item w JOIN state s ON s.id = w.state_id
            WHERE w.id = ANY(@ids) AND w.project_id = ANY(@projectIds)
            """,
            new { ids, projectIds = projectIds.ToArray() }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Id, r => new ItemFacts(r.Id, r.ProjectId, Enum.Parse<StateCategory>(r.CategoryText, ignoreCase: true), r.AssigneeId, r.ChangedAt));
    }

    // The same rule as ProjectResolver for a read with no project: the first of the user's recentProjects they still belong to, else the first by key.
    // Every membership row carries the user's recentProjects preference (one LEFT JOIN), so it needs no query of its own.
    private static Guid? RecentProject(List<Membership> memberships)
    {
        if (memberships.Count == 0)
        {
            return null;
        }

        foreach (var id in RecentProjects.Parse(memberships[0].Recent))
        {
            if (memberships.Exists(m => m.Id == id))
            {
                return id;
            }
        }

        return memberships[0].Id;
    }

    private sealed record Membership(Guid Id, string Key, string? Recent);

    private sealed record FactsRow(Guid Id, Guid ProjectId, string CategoryText, Guid? AssigneeId, DateTimeOffset ChangedAt);

    private static async Task<Dictionary<Guid, ItemRow>> DetailsAsync(NpgsqlConnection connection, Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<ItemRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.title, w.type, s.name AS state, s.category AS stateCategory, p.key AS projectKey,
                   u.name AS assignee, sp.name AS sprint, w.story_points AS points, w.tags AS tags
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            JOIN project p ON p.id = w.project_id
            LEFT JOIN app_user u ON u.id = w.assigned_to
            LEFT JOIN sprint sp ON sp.id = w.sprint_id
            WHERE w.id = ANY(@ids)
            """,
            new { ids }, cancellationToken: cancellationToken));
        var ancestors = await connection.QueryAsync<(Guid Root, string Key)>(new CommandDefinition(
            """
            WITH RECURSIVE chain AS (
                SELECT w.id AS root, w.parent_id AS ancestor, 1 AS depth FROM work_item w WHERE w.id = ANY(@ids) AND w.parent_id IS NOT NULL
                UNION ALL
                SELECT c.root, a.parent_id, c.depth + 1 FROM chain c JOIN work_item a ON a.id = c.ancestor WHERE a.parent_id IS NOT NULL)
            SELECT c.root, a.key FROM chain c JOIN work_item a ON a.id = c.ancestor ORDER BY c.root, c.depth DESC
            """,
            new { ids }, cancellationToken: cancellationToken));
        var paths = ancestors.GroupBy(a => a.Root).ToDictionary(g => g.Key, g => g.Select(a => a.Key).ToList());
        return rows.ToDictionary(r => r.Id, r => r with { ParentPath = paths.GetValueOrDefault(r.Id) ?? [] });
    }

    // Tags is an Array because that is the type Npgsql reports for text[], and Dapper matches the constructor on it.
    private sealed record ItemRow(Guid Id, string Key, string Title, string Type, string State, string StateCategory, string ProjectKey, string? Assignee, string? Sprint, decimal? Points, Array Tags)
    {
        public IReadOnlyList<string> ParentPath { get; init; } = [];

        public SearchResultItem ToItem(FusedItem fused) => new(
            Id,
            Key,
            Title,
            Enum.Parse<WorkItemTypeId>(Type, ignoreCase: true),
            State,
            Enum.Parse<StateCategory>(StateCategory, ignoreCase: true),
            ProjectKey,
            ParentPath,
            Assignee,
            Sprint,
            Points,
            (string[])Tags,
            fused.Best.Field,
            fused.Best.Attachment,
            fused.Best.Snippet.Length == 0 ? Title : fused.Best.Snippet,
            fused.Score);
    }
}

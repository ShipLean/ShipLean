using Dapper;
using Npgsql;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Application.Features.WorkItems.Reads.Tree;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Mcp.Planning.Reads;

// The plan reads over Postgres. Live (not Removed) comes from TreeSql; tags are read through PlanTags only.
public sealed class PlanReads(NpgsqlDataSource dataSource) : IPlanReads
{
    private const string ItemColumns =
        """
        w.id, w.parent_id AS parentId, w.key, w.title, w.description, s.name AS stateName, s.category AS stateCategory,
        u.name AS assignee, w.backlog_order AS rank, w.tags AS tags
        """;

    private const string ItemJoins =
        """
        JOIN state s ON s.id = w.state_id
        LEFT JOIN app_user u ON u.id = w.assigned_to
        """;

    public async Task<PlanTree> TreeAsync(Guid featureId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var head = await connection.QuerySingleOrDefaultAsync<FeatureRow>(new CommandDefinition(
            "SELECT w.id, w.project_id AS projectId, p.key AS projectKey, w.key, w.title, COALESCE(w.goal, '') AS goal, w.description, w.type, w.tags AS tags, s.category AS stateCategory FROM work_item w JOIN project p ON p.id = w.project_id JOIN state s ON s.id = w.state_id WHERE w.id = @featureId",
            new { featureId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that key was found.");
        if (head.Type != "feature")
        {
            throw ApiException.Validation($"{head.Key} is not a Feature, so it holds no plan.", "item");
        }
        if (Enum.Parse<StateCategory>(head.StateCategory, ignoreCase: true) == StateCategory.Removed)
        {
            throw ApiException.Validation($"{head.Key} was removed, so it holds no plan.", "item");
        }

        var storyRows = await connection.QueryAsync<PlanItemRow>(new CommandDefinition(
            $"SELECT {ItemColumns} FROM work_item w {ItemJoins} WHERE w.project_id = @projectId AND w.parent_id = @featureId AND w.type = 'story' AND {TreeSql.Live} ORDER BY {TreeSql.StackOrder}",
            new { featureId, projectId = head.ProjectId }, cancellationToken: cancellationToken));
        var stories = storyRows.Select(row => (Row: row, Tags: PlanTags.Parse((string[])row.Tags))).ToList();
        if (stories.Count == 0)
        {
            throw ApiException.Validation($"{head.Key} has no plan: it has no stories.", "item");
        }
        stories = WithWaves(stories);

        var taskRows = await connection.QueryAsync<PlanItemRow>(new CommandDefinition(
            $"SELECT {ItemColumns} FROM work_item w {ItemJoins} WHERE w.project_id = @projectId AND w.parent_id = ANY(@storyIds) AND w.type = 'task' AND {TreeSql.Live} ORDER BY {TreeSql.StackOrder}",
            new { projectId = head.ProjectId, storyIds = stories.Select(s => s.Row.Id).ToArray() }, cancellationToken: cancellationToken));
        var tasksByStory = taskRows.ToLookup(row => row.ParentId);

        var feature = new PlanFeature(head.Id, head.ProjectKey, head.Key, head.Title, head.Goal, head.Description, PlanTags.Parse((string[])head.Tags).Branch);
        var planStories = stories
            .OrderBy(s => s.Tags.Wave!) // a stable sort: stories of one wave stay in rank order
            .Select(s => new PlanStory(
                s.Row.Id, s.Row.Key, s.Row.Title, s.Tags.Wave!, s.Tags.Lane, s.Row.StateName, Category(s.Row), s.Row.Assignee, s.Row.Rank,
                [.. tasksByStory[s.Row.Id].Select(ToTask)]))
            .ToList();
        return new PlanTree(feature, planStories);
    }

    public async Task<(PlanTree Tree, Guid ItemId)> TreeOfAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        // The connection is released before TreeAsync opens its own, so one call never holds two.
        Lineage lineage;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            lineage = await connection.QuerySingleOrDefaultAsync<Lineage>(new CommandDefinition(
                """
                SELECT w.key, w.type, p.id AS parentId, p.type AS parentType, g.id AS grandparentId, g.type AS grandparentType
                FROM work_item w
                LEFT JOIN work_item p ON p.id = w.parent_id
                LEFT JOIN work_item g ON g.id = p.parent_id
                WHERE w.id = @itemId
                """,
                new { itemId }, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("No work item with that key was found.");
        }

        var featureId = lineage.Type switch
        {
            "feature" => itemId,
            "story" when lineage.ParentType == "feature" => lineage.ParentId,
            "task" when lineage.ParentType == "story" && lineage.GrandparentType == "feature" => lineage.GrandparentId,
            _ => null,
        };
        if (featureId is not { } feature)
        {
            throw ApiException.Validation($"{lineage.Key} is not in a plan: a plan is a Feature, its stories and their tasks.", "item");
        }

        var tree = await TreeAsync(feature, cancellationToken);
        var inTree = tree.Feature.Id == itemId
                     || tree.Stories.Any(story => story.Id == itemId || story.Tasks.Any(task => task.Id == itemId));
        return inTree
            ? (tree, itemId)
            : throw ApiException.Validation($"{lineage.Key} is not in a plan: it or its story was removed.", "item");
    }

    // A story with no wave: tag (one made by hand) is a wave of its own, after the tagged waves, in rank order.
    private static List<(PlanItemRow Row, PlanTaskTags Tags)> WithWaves(List<(PlanItemRow Row, PlanTaskTags Tags)> stories)
    {
        var next = stories.Select(s => s.Tags.Wave).OfType<WaveLabel>().Max()?.NextWhole() ?? WaveLabel.Parse("1");
        var waved = new List<(PlanItemRow Row, PlanTaskTags Tags)>(stories.Count);
        foreach (var story in stories)
        {
            if (story.Tags.Wave is null)
            {
                waved.Add(story with { Tags = story.Tags with { Wave = next } });
                next = next.NextWhole();
            }
            else
            {
                waved.Add(story);
            }
        }
        return waved;
    }

    private static PlanTask ToTask(PlanItemRow row)
    {
        var tags = PlanTags.Parse((string[])row.Tags);
        return new PlanTask(row.Id, row.Key, row.Title, row.Description, row.StateName, Category(row), row.Assignee, tags, tags.Orphan);
    }

    private static StateCategory Category(PlanItemRow row) => Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true);

    private sealed record FeatureRow(Guid Id, Guid ProjectId, string ProjectKey, string Key, string Title, string Goal, string Description, string Type, Array Tags, string StateCategory);

    private sealed record Lineage(string Key, string Type, Guid? ParentId, string? ParentType, Guid? GrandparentId, string? GrandparentType);
}

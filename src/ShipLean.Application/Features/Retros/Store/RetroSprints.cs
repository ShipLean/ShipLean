using Dapper;
using Npgsql;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Retros.Store;

// The sprint attribution rule: retrospective.sprint_id is the only thing that ties a retro to a sprint, and both writers set
// it through here when they create the retro. A sprint id gives itself. A story-level item gives its sprint now. A feature
// gives the sprint its last completed story-level child sat in when it completed. Anything else, and an item in no sprint, gives null.
public static class RetroSprints
{
    public static async Task<Guid?> ResolveAsync(NpgsqlConnection connection, Guid? workItemId, Guid? sprintId, CancellationToken cancellationToken)
    {
        if (sprintId is { } sprint)
        {
            return sprint;
        }

        if (workItemId is not { } itemId)
        {
            return null;
        }

        var item = await connection.QuerySingleOrDefaultAsync<ItemRow>(
            new CommandDefinition("SELECT type, sprint_id AS sprintId FROM work_item WHERE id = @itemId", new { itemId }, cancellationToken: cancellationToken));
        if (item is null)
        {
            return null;
        }

        if (item.Type == "feature")
        {
            return await FeatureSprintAsync(connection, itemId, cancellationToken);
        }

        return WorkItemTypeRules.StoryLevelDb.Contains(item.Type) ? item.SprintId : null;
    }

    private static async Task<Guid?> FeatureSprintAsync(NpgsqlConnection connection, Guid featureId, CancellationToken cancellationToken)
    {
        var children = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT id FROM work_item WHERE parent_id = @featureId AND type = ANY(@storyLevel)",
                new { featureId, storyLevel = WorkItemTypeRules.StoryLevelDb },
                cancellationToken: cancellationToken));
        var completions = await RetroSprintFacts.CompletionsAsync(connection, [.. children], cancellationToken);
        return completions.OrderByDescending(completion => completion.CompletedAt).FirstOrDefault()?.SprintId;
    }

    private sealed record ItemRow(string Type, Guid? SprintId);
}

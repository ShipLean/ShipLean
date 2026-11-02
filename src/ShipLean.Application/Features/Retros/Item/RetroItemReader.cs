using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Application.Features.Retros.Sprint;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Item;

// One item's retros for the item page. A task or action item has no retro of its own, so its
// key is not_found; any other item answers. The board holds the item's own retros; a Feature's totals and actions also count its stories' and bugs' points.
public sealed class RetroItemReader(NpgsqlDataSource dataSource, IRetroCards retroCards) : IRetroItemReader
{
    private static readonly string[] RetroTypes = ["epic", "feature", "story", "bug"];

    public async Task<RetroItemDto> GetAsync(Guid projectId, string itemKey, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            ItemSql + " WHERE w.project_id = @projectId AND w.key = @key AND w.type = ANY(@types)",
            new { projectId, key = itemKey.Trim().ToUpperInvariant(), types = RetroTypes },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("No work item with that key was found.");
        var isFeature = row.Type == "feature";

        var parent = row.ParentId is { } parentId && !isFeature
            ? await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
                ItemSql + " WHERE w.id = @parentId AND w.type = 'feature'", new { parentId }, cancellationToken: cancellationToken))
            : null;

        var children = isFeature
            ? (await connection.QueryAsync<ItemRow>(new CommandDefinition(
                ItemSql + " WHERE w.parent_id = @id AND w.type IN ('story', 'bug') ORDER BY w.number",
                new { id = row.Id }, cancellationToken: cancellationToken))).ToList()
            : [];

        var own = await retroCards.LoadAsync(await RetroIdsAsync(connection, [row.Id], cancellationToken), cancellationToken);
        var childRows = new List<RetroFeatureRowDto>();
        var childCards = new List<RetroCardDto>();
        foreach (var child in children)
        {
            var cards = await retroCards.LoadAsync(await RetroIdsAsync(connection, [child.Id], cancellationToken), cancellationToken);
            childCards.AddRange(cards);
            childRows.Add(new RetroFeatureRowDto(ToRef(child), ToState(child), cards.Count, cards.Count, RetroAggregates.Of(cards.SelectMany(card => card.Points)), cards.FirstOrDefault()?.CreatedAtLabel, false));
        }

        var rolledUp = own.Concat(childCards).SelectMany(card => card.Points).ToList();
        var actions = rolledUp
            .Select(point => point.Action)
            .OfType<RetroActionRefDto>()
            .GroupBy(action => action.WorkItemId)
            .Select(group => group.First())
            .ToList();
        return new RetroItemDto(
            ToRef(row),
            ToState(row),
            parent is null ? null : ToRef(parent),
            RetroAggregates.Of(rolledUp),
            SprintRetroColumns.Build(own, new Dictionary<Guid, DateTimeOffset>()),
            [.. own],
            actions,
            childRows);
    }

    private static async Task<IReadOnlyList<Guid>> RetroIdsAsync(NpgsqlConnection connection, Guid[] itemIds, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM retrospective WHERE work_item_id = ANY(@itemIds) AND archived_at IS NULL ORDER BY created_at DESC, id DESC",
            new { itemIds },
            cancellationToken: cancellationToken))];

    private static RetroItemRefDto ToRef(ItemRow row)
    {
        var type = Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true);
        return new RetroItemRefDto(row.Id, row.Key, new TypeRef(type, WorkItemTypeRules.Name(type)), row.Title);
    }

    private static StateRef ToState(ItemRow row)
        => new(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true));

    private const string ItemSql =
        """
        SELECT w.id, w.key, w.type, w.title, w.parent_id AS parentId, st.id AS stateId, st.name AS stateName, st.category AS stateCategory
        FROM work_item w JOIN state st ON st.id = w.state_id
        """;

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title, Guid? ParentId, Guid StateId, string StateName, string StateCategory);
}

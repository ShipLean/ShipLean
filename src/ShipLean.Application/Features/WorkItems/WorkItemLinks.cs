using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// Adds and removes an item's link rows. Removing archives the row; adding what was archived revives the same row.
internal static class WorkItemLinks
{
    // Writes the link part of a WorkItemChanges and returns the item's live links as they are afterwards.
    public static async Task<IReadOnlyList<LinkSnapshot>> ApplyAsync(
        WriteScope scope, Guid itemId, Guid projectId, Guid? actorId, IReadOnlyList<LinkSnapshot> current, WorkItemChanges? changes, CancellationToken cancellationToken)
    {
        if (changes is null || (changes.AddLinks.Count == 0 && changes.RemoveLinkIds.Count == 0))
        {
            return current;
        }

        var live = current.Select(l => l.Value).ToList();
        foreach (var id in changes.RemoveLinkIds)
        {
            var link = current.FirstOrDefault(l => l.Id == id) ?? throw ApiException.Validation("That link isn't on this work item.");
            live.Remove(link.Value);
            await ExecuteAsync(scope, "UPDATE work_item_link SET archived_at = @at WHERE id = @id", new { id, at = scope.At }, cancellationToken);
        }

        var stamped = 0;
        foreach (var added in changes.AddLinks)
        {
            var value = await NormaliseAsync(scope, itemId, projectId, added, cancellationToken);
            if (live.Contains(value))
            {
                continue;
            }

            live.Add(value);
            if (await AddAsync(scope, itemId, actorId, value, scope.At.AddTicks(stamped * TimeSpan.TicksPerMicrosecond), cancellationToken))
            {
                stamped++;
            }
        }

        var rows = await WorkItemQueries.LinksAsync(scope.Connection, scope.Transaction, [itemId], cancellationToken);
        return rows.GetValueOrDefault(itemId) ?? [];
    }

    // Inserts the link, stamped at (the write's moment + one microsecond per link already inserted by it), so links read back
    // in the order they were added. A revived link keeps its original stamp and so its place. Returns whether a row was inserted.
    private static async Task<bool> AddAsync(WriteScope scope, Guid itemId, Guid? actorId, LinkValue value, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        var type = WorkItemLinkRow.TypeToDb(value.Type);
        var archived = await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT id FROM work_item_link
            WHERE source_id = @itemId AND archived_at IS NOT NULL AND link_type = @type
              AND target_id IS NOT DISTINCT FROM @target AND url IS NOT DISTINCT FROM @url AND title IS NOT DISTINCT FROM @title
            ORDER BY archived_at DESC, id LIMIT 1
            """,
            new { itemId, type, target = value.WorkItemId, url = value.Url, title = value.Title }, scope.Transaction, cancellationToken: cancellationToken));
        if (archived is { } revived)
        {
            await ExecuteAsync(scope, "UPDATE work_item_link SET archived_at = NULL WHERE id = @id", new { id = revived }, cancellationToken);
            return false;
        }

        await ExecuteAsync(
            scope,
            """
            INSERT INTO work_item_link (id, source_id, target_id, link_type, url, title, created_by, created_at)
            VALUES (@id, @itemId, @target, @type, @url, @title, @actorId, @at)
            """,
            new { id = Guid.NewGuid(), itemId, target = value.WorkItemId, type, url = value.Url, title = value.Title, actorId, at = createdAt },
            cancellationToken);
        return true;
    }

    private static async Task<LinkValue> NormaliseAsync(WriteScope scope, Guid itemId, Guid projectId, LinkValue value, CancellationToken cancellationToken)
    {
        if (value.Type == LinkType.Url)
        {
            return string.IsNullOrWhiteSpace(value.Url) || value.WorkItemId is not null
                ? throw ApiException.Validation("A url link needs a url and no work item.")
                : new LinkValue(LinkType.Url, null, null, value.Url, value.Title);
        }

        if (value.WorkItemId is not { } targetId)
        {
            throw ApiException.Validation("A link between work items needs the other work item.");
        }

        var target = await scope.Connection.QuerySingleOrDefaultAsync<TargetRow>(new CommandDefinition(
            "SELECT key, project_id AS projectId FROM work_item WHERE id = @id", new { id = targetId }, scope.Transaction, cancellationToken: cancellationToken));
        if (target is null || target.ProjectId != projectId)
        {
            throw ApiException.NotFound("Linked work item not found.");
        }

        return targetId == itemId
            ? throw ApiException.Validation("A work item can't link to itself.")
            : new LinkValue(value.Type, targetId, target.Key, null, null);
    }

    private static Task ExecuteAsync(WriteScope scope, string sql, object parameters, CancellationToken cancellationToken)
        => scope.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, scope.Transaction, cancellationToken: cancellationToken));

    private sealed record TargetRow(string Key, Guid ProjectId);
}

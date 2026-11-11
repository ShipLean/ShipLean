using Dapper;
using Npgsql;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Backlog.NeedsYou;

// SHIP-922's open questions and decisions waiting on a user, per item, in one query. An ask is the user's when AskOwnerSql.OwnedByUser
// matches its item. A Feature also counts the asks on the stories and bugs directly under it (OnChildren); nested Features are their own
// rows. An item with nothing waiting is left out.
public sealed class NeedsYouCounts(NpgsqlDataSource dataSource) : INeedsYouCounts
{
    public async Task<IReadOnlyDictionary<Guid, NeedsYouDto>> ForItemsAsync(Guid userId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, NeedsYouDto>();
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AskRow>(new CommandDefinition(
            $"""
            SELECT target.id AS targetId, (wi.id <> target.id) AS fromChild,
                   (SELECT count(*) FROM question q WHERE q.work_item_id = wi.id AND q.state = 'open')::int AS questions,
                   (SELECT count(*) FROM decision d WHERE d.work_item_id = wi.id AND d.state = 'open')::int AS decisions
            FROM work_item target
            JOIN work_item wi ON wi.id = target.id
                              OR (target.type = 'feature' AND wi.parent_id = target.id AND wi.type IN ('story', 'bug'))
            WHERE target.id = ANY(@ids) AND {AskOwnerSql.OwnedByUser}
            """,
            new { userId, ids = itemIds.ToArray() }, cancellationToken: cancellationToken));
        return rows
            .Where(r => r.Questions + r.Decisions > 0)
            .GroupBy(r => r.TargetId)
            .ToDictionary(g => g.Key, g => new NeedsYouDto(g.Sum(r => r.Questions), g.Sum(r => r.Decisions), g.Any(r => r.FromChild)));
    }

    private sealed record AskRow(Guid TargetId, bool FromChild, int Questions, int Decisions);
}

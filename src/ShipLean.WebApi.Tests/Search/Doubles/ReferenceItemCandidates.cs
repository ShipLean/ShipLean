using Dapper;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// A deliberately plain lexical leg that proves ItemCandidatesContract's plumbing: it scores an item by how many of its rows contain
// the text, uses no tsv, and joins project_member and state itself. Real legs replace it in their own subclasses.
public sealed class ReferenceItemCandidates(NpgsqlDataSource dataSource, ICurrentUser currentUser) : IItemCandidates
{
    public CandidateLeg Leg => CandidateLeg.Lexical;

    public async Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid WorkItemId, double Score)>(
            new CommandDefinition(
                """
                SELECT d.work_item_id AS WorkItemId, count(*)::float8 AS Score
                FROM search_doc d
                JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
                JOIN work_item w ON w.id = d.work_item_id
                JOIN state s ON s.id = w.state_id
                WHERE d.project_id = ANY(@projectIds) AND s.category <> 'removed' AND d.text ILIKE '%' || @text || '%'
                GROUP BY d.work_item_id
                ORDER BY Score DESC, d.work_item_id
                """,
                new { me = currentUser.Id, projectIds = query.ProjectIds.ToArray(), text = query.Text },
                cancellationToken: cancellationToken));
        var hits = rows.Select(r => new CandidateHit(r.WorkItemId, SearchField.Title, r.Score, "")).ToList();
        return new CandidateLists(true, 0, [new RankedList(RankedListName.TitleLexical, hits)]);
    }
}

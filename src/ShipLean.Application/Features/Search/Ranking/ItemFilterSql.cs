using Dapper;
using ShipLean.Contracts.Criteria;

namespace ShipLean.Application.Features.Search.Ranking;

// The item filters of a candidate query as SQL on w (work_item) and s (state): closed, types, tags (all of them, whole tag,
// case-insensitive, the way criteria compare tags) and criteria. Removed items and access are each leg's own joins.
internal static class ItemFilterSql
{
    public const string Alias = "w";

    // A fragment starting with " AND ", or empty. Every value is added to parameters.
    public static string Build(CandidateQuery query, ICriteriaEvaluator criteria, DynamicParameters parameters)
    {
        var filters = string.Empty;
        if (!query.IncludeClosed)
        {
            filters += " AND s.category <> 'completed'";
        }

        if (query.Types is { Count: > 0 } types)
        {
            filters += " AND w.type = ANY(@types)";
            parameters.Add("types", types.Select(t => t.ToString().ToLowerInvariant()).ToArray());
        }

        if (query.Tags is { Count: > 0 } tags)
        {
            filters += " AND NOT EXISTS (SELECT 1 FROM unnest(@tags) AS wanted(tag) WHERE NOT EXISTS (SELECT 1 FROM unnest(w.tags) AS have(tag) WHERE lower(have.tag) = lower(wanted.tag)))";
            parameters.Add("tags", tags.ToArray());
        }

        if (query.Criteria is not null)
        {
            var sql = criteria.ToSql(query.Criteria, Alias);
            filters += $" AND ({sql.Where})";
            foreach (var (name, value) in sql.Parameters)
            {
                parameters.Add(name, value);
            }
        }

        return filters;
    }
}

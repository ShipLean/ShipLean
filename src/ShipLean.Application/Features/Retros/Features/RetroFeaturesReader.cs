using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.Features;

// The retros of a project rolled up by Feature, in one query. A Feature's row counts its own retro and its stories' and bugs'
// (decision 19); a story or bug with no Feature that has a retro is loose. Archived retros and points are left out. Decisions
// follow contract K2 through RetroDecisionSql, so Went well is never decidable and a Removed action returns its points to undecided.
public sealed class RetroFeaturesReader(NpgsqlDataSource dataSource, TimeProvider clock) : IRetroFeaturesReader
{
    public async Task<RetroFeaturesDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<RetroFeatureQueryRow>(new CommandDefinition(Sql, new { projectId }, cancellationToken: cancellationToken))).ToList();
        return new RetroFeaturesDto(
            [.. rows.Where(row => !row.IsLoose).Select(ToDto)],
            [.. rows.Where(row => row.IsLoose).Select(ToDto)]);
    }

    private RetroFeatureRowDto ToDto(RetroFeatureQueryRow row)
    {
        var type = Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true);
        var counts = new RetroCategoryCountsDto(row.WentWell, row.NotInPlan, row.WentBadly, row.Improve);
        return new RetroFeatureRowDto(
            new RetroItemRefDto(row.Id, row.Key, new TypeRef(type, WorkItemTypeRules.Name(type)), row.Title),
            new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true)),
            row.Retros,
            row.OwnRetros,
            new RetroAggregateDto(counts, row.ToDecide, row.Decidable - row.ToDecide, row.Decidable, row.Actions),
            DisplayDate.Relative(new DateTimeOffset(DateTime.SpecifyKind(row.LastRetroAt, DateTimeKind.Utc)), clock),
            !row.IsLoose && row.OwnRetros == 0);
    }

    private static readonly string Sql =
        $"""
        WITH retro_root AS (
            SELECT rt.id AS retro_id, rt.created_at,
                   COALESCE(pf.id, w.id) AS root_id,
                   COALESCE(pf.id, w.id) = w.id AS own
            FROM retrospective rt
            JOIN work_item w ON w.id = rt.work_item_id AND w.project_id = @projectId AND w.type IN ('feature', 'story', 'bug')
            LEFT JOIN work_item pf ON pf.id = w.parent_id AND pf.type = 'feature' AND w.type <> 'feature'
            WHERE rt.archived_at IS NULL
        ),
        retro_totals AS (
            SELECT root_id, count(*)::int AS retros, (count(*) FILTER (WHERE own))::int AS own_retros, max(created_at) AS last_retro_at
            FROM retro_root GROUP BY root_id
        ),
        point_totals AS (
            SELECT rr.root_id,
                   (count(*) FILTER (WHERE p.category = 'wentWell'))::int AS went_well,
                   (count(*) FILTER (WHERE p.category = 'notInPlan'))::int AS not_in_plan,
                   (count(*) FILTER (WHERE p.category = 'wentBadly'))::int AS went_badly,
                   (count(*) FILTER (WHERE p.category = 'improve'))::int AS improve,
                   (count(*) FILTER (WHERE {RetroDecisionSql.Undecided("p")}))::int AS to_decide,
                   (count(*) FILTER (WHERE p.category <> 'wentWell'))::int AS decidable,
                   count(DISTINCT al.work_item_id)::int AS actions
            FROM retro_root rr
            JOIN retro_point p ON p.retrospective_id = rr.retro_id AND p.archived_at IS NULL
            LEFT JOIN LATERAL (
                SELECT rap.work_item_id FROM retro_action_point rap
                JOIN work_item aw ON aw.id = rap.work_item_id
                JOIN state ast ON ast.id = aw.state_id AND ast.category <> 'removed'
                WHERE rap.point_id = p.id AND rap.archived_at IS NULL
            ) al ON true
            GROUP BY rr.root_id
        )
        SELECT w.id, w.key, w.type, w.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory,
               w.type <> 'feature' AS isLoose, rt.retros, rt.own_retros AS ownRetros, rt.last_retro_at AS lastRetroAt,
               COALESCE(pt.went_well, 0) AS wentWell, COALESCE(pt.not_in_plan, 0) AS notInPlan,
               COALESCE(pt.went_badly, 0) AS wentBadly, COALESCE(pt.improve, 0) AS improve,
               COALESCE(pt.to_decide, 0) AS toDecide, COALESCE(pt.decidable, 0) AS decidable, COALESCE(pt.actions, 0) AS actions
        FROM retro_totals rt
        JOIN work_item w ON w.id = rt.root_id
        JOIN state st ON st.id = w.state_id
        LEFT JOIN point_totals pt ON pt.root_id = rt.root_id
        ORDER BY w.number, w.id
        """;
}

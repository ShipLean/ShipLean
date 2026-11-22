using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Decide;

// The scope rule of the decide planner and of the aggregate export as SQL, so the text is built once: the live points of a project that fall in one scope.
// A Feature's scope is its own retro plus its stories' and bugs'; a date range is UTC days, both ends included, by the day the point was logged.
// Aliases: p = retro_point, r = retrospective, w = the retro's work item. Parameters: see Args.
internal static class DecideScopeSql
{
    public const string From = "retro_point p JOIN retrospective r ON r.id = p.retrospective_id LEFT JOIN work_item w ON w.id = r.work_item_id";

    public const string Where = """
        r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
          AND (@sprintId::uuid IS NULL OR r.sprint_id = @sprintId)
          AND (@featureId::uuid IS NULL OR r.work_item_id = @featureId OR (w.parent_id = @featureId AND w.type IN ('story', 'bug')))
          AND (@since::timestamptz IS NULL OR p.created_at >= @since)
          AND (@until::timestamptz IS NULL OR p.created_at < @until)
        """;

    public static object Args(Guid projectId, DecideScope scope)
        => new
        {
            projectId,
            sprintId = scope.SprintId,
            featureId = scope.FeatureId,
            since = scope.From is { } from ? Utc(from) : (DateTime?)null,
            until = scope.To is { } to ? Utc(to.AddDays(1)) : (DateTime?)null,
        };

    private static DateTime Utc(DateOnly day) => new(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc);
}

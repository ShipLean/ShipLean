namespace ShipLean.Application.Features.Retros.Reads;

// SQL about an action item that more than one reader needs.
internal static class RetroActionSql
{
    // An integer expression: the live linked points of the action item (alias of retro_action): not archived join rows over points that are not archived.
    public static string PointCount(string action)
        => $"""
            (SELECT count(*)::int FROM retro_action_point lp JOIN retro_point lpp ON lpp.id = lp.point_id
             WHERE lp.work_item_id = {action}.work_item_id AND lp.archived_at IS NULL AND lpp.archived_at IS NULL)
            """;
}

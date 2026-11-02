namespace ShipLean.Application.Features.Retros.Reads;

// The decision rule of a point (contract K2) as SQL, for readers that filter or count in the database. RetroCards is the one
// reader that builds the DTO. A live link is a join row that is not archived whose action item is not in a Removed state, so
// moving the action item to Removed returns its points to undecided with no write.
public static class RetroDecisionSql
{
    // A boolean expression: the point (alias) has a live link.
    public static string Actioned(string point)
        => $"""
            EXISTS (SELECT 1 FROM retro_action_point rap
                    JOIN work_item aw ON aw.id = rap.work_item_id
                    JOIN state ast ON ast.id = aw.state_id
                    WHERE rap.point_id = {point}.id AND rap.archived_at IS NULL AND ast.category <> 'removed')
            """;

    // A text expression: null for wentWell, else 'actioned', 'notNeeded' or 'undecided'.
    public static string Decision(string point)
        => $"""
            CASE WHEN {point}.category = 'wentWell' THEN NULL
                 WHEN {Actioned(point)} THEN 'actioned'
                 WHEN {point}.not_needed_reason IS NOT NULL THEN 'notNeeded'
                 ELSE 'undecided' END
            """;

    // A boolean expression: the point still needs a decision.
    public static string Undecided(string point) => $"{Decision(point)} = 'undecided'";
}

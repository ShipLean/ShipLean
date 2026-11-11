namespace ShipLean.Contracts.WorkItems;

// Whose an ask is, as one SQL predicate the Inbox's sources share. It is the rule in WaitingOnMe (Features/Asks/Waiting): the owner is
// coalesce(assigned_to, created_by), never "assigned to or created by"; the owner must still be a member of the item's project
// (project_member.archived_at IS NULL); and a Removed item drops out. Use it in a WHERE clause over a work_item aliased `wi`, with a
// parameter @userId. It is EXISTS subqueries rather than JOINs, so it adds no rows and needs no other alias.
public static class AskOwnerSql
{
    public const string OwnedByUser =
        """
        (coalesce(wi.assigned_to, wi.created_by) = @userId
         AND EXISTS (SELECT 1 FROM project_member owner_member
                     WHERE owner_member.project_id = wi.project_id AND owner_member.user_id = @userId AND owner_member.archived_at IS NULL)
         AND EXISTS (SELECT 1 FROM state owner_state WHERE owner_state.id = wi.state_id AND owner_state.category <> 'removed'))
        """;
}

namespace ShipLean.Core.Sprints;

// What "the sprint running now" means in SQL, shared by SprintResolver ("current") and the project list: a sprint (aliased
// s) that is not archived and whose dates take in @today (UTC). The caller binds @today and orders by start_date.
public static class CurrentSprintSql
{
    public const string Where = "s.archived_at IS NULL AND s.start_date <= @today AND s.end_date >= @today";
}

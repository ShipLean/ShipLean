using Dapper;

namespace ShipLean.Application.Features.WorkItems;

// One writer per project at a time. Every write to work items (create, patch, patch-many, move, reorder, remove, import)
// takes this lock before it locks a row or reads an order, so crossing links and reparents cannot deadlock, the ancestor
// check sees the other writer's committed parent, and two writes never compute the same order key.
//
// The key is the two-int form pg_advisory_xact_lock(NAMESPACE, hashtext(project id)). Postgres keeps that form apart from
// the one-bigint form the sprint store uses for its per-project lock, and NAMESPACE keeps it apart from any other two-int user.
// The real order in a sprint rollover is: this lock, then the item rows, then the sprint row (its InTransaction hook).
// So never call IWorkItemStore while holding the sprint store's advisory lock or a sprint row lock, and never take the sprint
// lock inside a work item InTransaction hook. A batch spanning projects takes the locks in lock-key order (two projects that share a key take it once). The lock is held
// to the end of the transaction.
internal static class WorkItemProjectLock
{
    private const int NAMESPACE = 0x57_49_53_4C;

    public static async Task TakeAsync(WriteScope scope, IEnumerable<Guid> projectIds, CancellationToken cancellationToken)
    {
        var keys = await scope.Connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DISTINCT hashtext(p::text) AS key FROM unnest(@projectIds) AS p ORDER BY key",
            new { projectIds = projectIds.Distinct().ToArray() }, scope.Transaction, cancellationToken: cancellationToken));
        foreach (var key in keys)
        {
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(@namespace, @key)",
                new { @namespace = NAMESPACE, key }, scope.Transaction, cancellationToken: cancellationToken));
        }
    }

    // The projects of the given items, read without a row lock.
    public static async Task TakeForItemsAsync(WriteScope scope, Guid[] itemIds, CancellationToken cancellationToken)
    {
        var projectIds = await scope.Connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT DISTINCT project_id FROM work_item WHERE id = ANY(@itemIds)",
            new { itemIds }, scope.Transaction, cancellationToken: cancellationToken));
        await TakeAsync(scope, projectIds, cancellationToken);
    }
}

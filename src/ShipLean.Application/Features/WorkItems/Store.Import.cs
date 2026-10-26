using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    // The importer's write: a Jira-origin transaction that takes the project lock first, then creates the item or, when the
    // original system id is already stored, brings it up to date. The rules never run: the item is what the source system says.
    public Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default)
        => WriteAsync(new WriteOptions(null, RevisionOrigin.Jira), async scope =>
        {
            await WorkItemProjectLock.TakeAsync(scope, [projectId], cancellationToken);
            var existing = await WorkItemQueries.RowsAsync(
                scope.Connection,
                scope.Transaction,
                $"{WorkItemRow.Select} WHERE w.project_id = @projectId AND w.original_system = @system AND w.original_system_id = @externalId FOR UPDATE OF w",
                new { projectId, system = item.OriginalSystem.System, externalId = item.OriginalSystem.Id },
                cancellationToken);
            var result = existing.Count == 0
                ? await ImportNewAsync(scope, projectId, importJobId, item, cancellationToken)
                : await ImportExistingAsync(scope, existing[0], importJobId, item, cancellationToken);
            return (result, true);
        }, cancellationToken);
}

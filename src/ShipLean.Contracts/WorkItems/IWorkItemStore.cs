
namespace ShipLean.Contracts.WorkItems;

// The only writer of work items. Every write stamps ICurrentUser.Id, bumps the revision once and records the changed
// fields as FieldChange[]; a write that changes nothing records nothing. Stores do not check access: endpoints call
// IProjectAccess first, and the store trusts its caller. A stale expectedRevision is not an exception: it returns a
// ConflictDto built by the store (endpoints turn it into the 409 body). Position-only changes (a reorder, a move that
// changes no field) write no revision. A revision's changes are history fields only (title, type, state, assignee,
// priority, points, tags, sprint, parent, severity, description, acceptanceCriteria, reproSteps, goal, statusLine, statusVerifiedAt, links), in field-ids.json
// order; ShipLean.Application.WorkItems.History.WorkItemFieldDiff computes them for every implementation.
public interface IWorkItemStore
{
    // Throws ApiException.NotFound (unknown project, parent or linked item), ApiException.Validation (blank title, parent type not allowed for the type, bad field value).
    Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default);

    // Several creates in one transaction: keys are allocated in input order and the outcomes come back in input order.
    // All or nothing: any item that throws as CreateAsync does writes none of them. The InTransaction hook runs once per
// batch, not once per item.
    Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default);

    // Throws ApiException.NotFound (unknown item, parent, link target or user), ApiException.Validation (blank title, disallowed parent type, bad field value).
    Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default);

    // All or nothing. Throws as PatchAsync; any throw or stale item writes nothing for any item.
    Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default);

    // Position, optional lane, order before another card of the column, and the Set patch, in one revision. Throws as PatchAsync, plus NotFound for an unknown BeforeItemId.
    Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default);

    // Backlog order: before the given item, or last when null. Throws ApiException.NotFound (unknown item or BeforeItemId).
    Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default);

    // Moves to the type's first Removed-category state; nothing is deleted. Throws ApiException.NotFound (unknown item), ApiException.Validation (type has no Removed state).
    Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default);

    // Upsert keyed on original system + id within the project; keeps the key number, advances the project's sequence past
    // it, writes the imported revisions and never runs the rules. Throws ApiException.NotFound (unknown project),
    // ApiException.Validation (disallowed parent type), ApiException.Conflict (the key number belongs to another item).
    Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default);

    // Throws ApiException.NotFound.
    Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default);

    // Oldest first. Throws ApiException.NotFound.
    Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default);
}

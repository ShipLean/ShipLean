using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems.Reads.Tree;

// The one set of tree reads that backlog, boards and agent tools build over, so a roll-up or a stack order means the same
// thing everywhere. Callers check project access first. Every read is scoped to the project id: another project's ids
// give nothing. Removed items are never returned or counted. Closed = Completed category.
//
// Walking through Removed items: a Removed item is dropped from the results and the counts, but its children are still
// reached and counted (they are live work), so removing a Feature does not hide its stories from the Epic above it.
//
// Tree mode (the backlog grouped under Epic -> Feature -> nested Feature) is stitched by the caller: StackAsync once per
// level, joined on StackRowDto.Parent / ParentId, with RollupsAsync for the group totals. A Feature that the filter hides,
// or that is completed, can still hold open stories that need its group row, so the group levels (Feature, Epic) are read
// with includeCompleted: true and no filter; only the Requirement level carries the view's filter and includeCompleted.
public interface IWorkTreeReads
{
    // Every descendant of the item, at any depth: open first, then closed, each group in stack order. Optionally one type only.
    Task<IReadOnlyList<WorkTreeItemDto>> DescendantsAsync(Guid projectId, Guid itemId, WorkItemTypeId? type, CancellationToken cancellationToken = default);

    // One entry per parent and descendant type that has any. The filter, when given, keeps only the descendants that pass it.
    // Throws ApiException.Validation (the criteria are invalid).
    Task<IReadOnlyList<WorkTreeRollupDto>> RollupsAsync(Guid projectId, IReadOnlyCollection<Guid> parentIds, CriteriaJson? filter, CancellationToken cancellationToken = default);

    // The level's items in stack order (backlog order, then id). Completed items are left out unless includeCompleted.
    // Throws ApiException.Validation (the criteria are invalid).
    Task<IReadOnlyList<StackRowDto>> StackAsync(Guid projectId, StackLevel level, CriteriaJson? filter, bool includeCompleted, CancellationToken cancellationToken = default);

    // Items in the sprint that nobody is assigned to and whose state is in the Proposed category, in stack order. The parent
    // filter matches at any depth: an Epic finds the stories under its Features, a Feature the tasks under its stories.
    Task<IReadOnlyList<WorkTreeItemDto>> ReadyAsync(Guid projectId, Guid sprintId, WorkItemTypeId? type, Guid? parentId, CancellationToken cancellationToken = default);
}

using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// New items under ParentId (an existing item: a Feature gaining Stories), or with no parent when null. A new root is one
// item in Items whose Children are its Stories, and so on down.
public sealed record WorkItemTreeCreate(Guid ProjectId, Guid? ParentId, IReadOnlyList<WorkItemTreeCreate.Node> Items)
{
    // One new item as WorkItemCreate describes it, without the project, parent or sprint: the tree gives the first two,
    // and Changes can set the sprint.
    public sealed record Node(WorkItemTypeId Type, string Title, WorkItemChanges? Changes, IReadOnlyList<Node> Children)
    {
        // The create for this item under parentId. Its parent comes from the tree, so Changes may not set one.
        public WorkItemCreate CreateUnder(Guid projectId, Guid? parentId) => Changes?.Parent is null
            ? new(projectId, Type, Title, parentId, null, Changes)
            : throw ApiException.Validation("An item in a tree takes its parent from the tree.", "parent");
    }
}

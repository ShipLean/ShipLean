using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems.Reads.Tree;

// One item of a descendants or ready read. ParentId is the item's parent (null for a root). Depth is how far below the item
// asked about this one sits: 1 = a direct child, 2 = a grandchild and so on, so a caller can pick the direct children out of
// all descendants and see which nested Feature an item sits under. A ready read without a parent filter has no such item: Depth is 0.
// Revision is the item's current revision.
public sealed record WorkTreeItemDto(
    Guid Id,
    string Key,
    string Title,
    TypeRef Type,
    StateRef State,
    SprintRef? Sprint,
    decimal? Points,
    UserRef? Assignee,
    Guid? ParentId,
    int Depth,
    int Revision);

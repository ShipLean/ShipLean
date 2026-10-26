namespace ShipLean.Contracts.Views;

// Item: a work item. Group: a work item in tree mode whose children follow it. NoParentGroup: the synthetic "No parent" group at
// the end of a tree, which holds the items without a parent; it is not a work item.
public enum BacklogRowKind
{
    Item,
    Group,
    NoParentGroup,
}

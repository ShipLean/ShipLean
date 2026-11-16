using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Links;

// One live link to another work item, with the other item's key, title, type and state. Incoming is false for a link the item
// owns and true for a link another item made to it: today only a Caused by link, shown as Caused on the task a Bug points at.
public sealed record WorkItemLinkRowDto(Guid Id, LinkType Type, Guid WorkItemId, string Key, string Title, TypeRef ItemType, StateRef State, bool Incoming);

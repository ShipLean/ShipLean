using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Links;

// One live link to another work item, with the other item's key, title, type and state.
public sealed record WorkItemLinkRowDto(Guid Id, LinkType Type, Guid WorkItemId, string Key, string Title, TypeRef ItemType, StateRef State);

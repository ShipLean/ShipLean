using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Links;

// POST api/work-items/{id}/work-item-links. TargetKey is the other item's key in the same project (SHIP-142); Type is not Url.
public sealed record AddWorkItemLinkRequest(string TargetKey, LinkType Type);

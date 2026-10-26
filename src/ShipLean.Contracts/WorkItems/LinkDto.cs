using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

// One live link row. A work item link carries the target's id, key and title; a url link carries Url and Title, and
// Card when a webhook has reported on the pull or merge request behind it. AutoLinked is set on a link a webhook added.
public sealed record LinkDto(Guid Id, LinkType Type, Guid? WorkItemId, string? Key, string? Title, string? Url, LinkCardDto? Card = null, AutoLinkDto? AutoLinked = null);

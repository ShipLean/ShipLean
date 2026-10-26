namespace ShipLean.Contracts.Links;

// A work item's live links: the ones to other work items, and the URL links split into code (pull requests, merge requests,
// issues) and pages. Each list is oldest first.
public sealed record LinkCardsDto(IReadOnlyList<WorkItemLinkRowDto> WorkItems, IReadOnlyList<LinkCardDto> Code, IReadOnlyList<LinkCardDto> Pages);

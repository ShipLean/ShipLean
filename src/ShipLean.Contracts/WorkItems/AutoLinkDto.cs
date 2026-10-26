namespace ShipLean.Contracts.WorkItems;

// Why a webhook added a link: the provider, and whether the item's key was in the title or the source branch.
public sealed record AutoLinkDto(WebhookProvider Via, AutoLinkMatch MatchedOn);

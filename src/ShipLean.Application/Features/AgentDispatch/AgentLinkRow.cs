namespace ShipLean.Application.Features.AgentDispatch;

// A live link of the item. TargetKey and TargetProjectKey are set for an item link, Url for a url link.
internal sealed record AgentLinkRow(string Type, string? TargetKey, string? TargetProjectKey, string? TargetTitle, string? Url, string? Title);

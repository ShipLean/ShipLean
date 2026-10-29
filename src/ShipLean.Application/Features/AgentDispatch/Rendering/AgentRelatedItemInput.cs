namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The parent item or the ancestor Feature, or one link of the ancestor chain (Type is then the work item type, e.g. "feature").
public sealed record AgentRelatedItemInput(string Id, string Key, string Title, string? Description, string Url, string? Type = null);

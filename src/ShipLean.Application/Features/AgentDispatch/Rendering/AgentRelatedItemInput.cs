namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The parent item or the ancestor Feature.
public sealed record AgentRelatedItemInput(string Id, string Key, string Title, string? Description, string Url);

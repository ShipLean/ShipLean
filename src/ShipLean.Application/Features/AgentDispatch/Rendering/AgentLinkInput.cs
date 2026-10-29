namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// A link on the item. Type is the link type as stored (blockedBy, related, url ...). An item link carries the other item's key and a
// url to it; a url link has no identifier and its own url.
public sealed record AgentLinkInput(string Type, string? Identifier, string? Title, string Url);

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// A child of the item, by state name.
public sealed record AgentChildInput(string Key, string Title, string State, string Url);

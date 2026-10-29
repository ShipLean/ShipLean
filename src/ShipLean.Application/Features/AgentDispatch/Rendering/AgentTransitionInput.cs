namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The state move that started a session, by state name; From is "Any" for an Any-state transition.
public sealed record AgentTransitionInput(string From, string To);

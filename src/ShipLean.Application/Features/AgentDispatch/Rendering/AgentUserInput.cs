namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// A person as the agent sees them: the session creator (the relay allow-lists on Email) or a comment/prompt author.
public sealed record AgentUserInput(string Id, string Name, string Email);

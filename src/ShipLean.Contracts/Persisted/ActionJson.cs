namespace ShipLean.Contracts.Persisted;

// One thing a transition does. Kind is "agent" (send to an agent); ConnectionId null means the project's connection.
public sealed record ActionJson(string Id, string Kind, string Prompt, Guid? ConnectionId);

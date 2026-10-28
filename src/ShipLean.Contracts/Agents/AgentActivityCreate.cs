namespace ShipLean.Contracts.Agents;

// One activity an agent reports. Type is thought, action, elicitation, response or error (never prompt: only people prompt).
// Body is the text of every type but action; Action, Parameter and Result are an action's. Ephemeral is allowed on thought and
// action only. Id is the agent's own id for the activity, when it sends one.
public sealed record AgentActivityCreate(
    Guid SessionId, string Type, string? Body, string? Action, string? Parameter, string? Result, bool Ephemeral, string? Signal, Guid? Id);

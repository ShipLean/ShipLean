namespace ShipLean.Contracts.Agents;

// One line of the agent's log. Type is thought, action, elicitation, response, error or prompt. Body is the text (null for an
// action); Action, Parameter and Result describe an action. Signal is set on a prompt that carries one (stop). Text is untrusted.
public sealed record AgentActivityDto(
    Guid Id,
    string Type,
    string? Body,
    string? Action,
    string? Parameter,
    string? Result,
    bool Ephemeral,
    string? Signal,
    DateTimeOffset CreatedAt,
    string CreatedLabel);

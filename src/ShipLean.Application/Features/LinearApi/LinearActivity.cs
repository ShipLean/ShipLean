namespace ShipLean.Application.Features.LinearApi;

// One row of a session's log. Type is thought, action, elicitation, response, error or prompt.
public sealed record LinearActivity(
    Guid Id,
    Guid SessionId,
    string Type,
    string? Body,
    string? Action,
    string? Parameter,
    string? Result,
    bool Ephemeral,
    string? Signal,
    DateTimeOffset CreatedAt);

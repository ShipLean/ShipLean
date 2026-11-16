namespace ShipLean.Contracts.Agents;

// Test connection's result: Reached is true for any HTTP answer, and HttpStatus says which. Error is scrubbed, set when no
// answer came back.
public sealed record AgentConnectionTestDto(bool Reached, int? HttpStatus, string? Error, int DurationMs);

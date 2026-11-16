namespace ShipLean.Contracts.Inspection;

public sealed record AgentRunsTotalsDto(int Runs, long TookMs, long? TokensIn, long? TokensOut, int Errors, long WaitMs);

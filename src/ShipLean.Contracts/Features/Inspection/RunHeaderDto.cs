using ShipLean.Contracts.Prompts;

namespace ShipLean.Contracts.Inspection;

public sealed record RunHeaderDto(
    string Agent,
    string AgentLabel,
    string StartedLabel,
    string? StartedBy,
    LinkSource LinkSource,
    string? OlderRunId,
    string? NewerRunId,
    long TookMs,
    int Turns,
    int SpansOrEvents,
    long? TokensIn,
    long? TokensOut,
    int Errors,
    long WaitMs,
    IReadOnlyList<PromptServingDto> Prompts);

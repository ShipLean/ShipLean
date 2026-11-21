namespace ShipLean.Contracts.Prompts;

public sealed record PromptsPageDto(
    IReadOnlyList<PromptStageDto> Stages,
    int CustomCount,
    bool CanEdit,
    IReadOnlyList<string> AdminNames,
    IReadOnlyList<PromptAgentDto> Agents,
    IReadOnlyList<PromptBuiltInOnlyDto> BuiltInOnly);

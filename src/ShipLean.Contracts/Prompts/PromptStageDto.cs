namespace ShipLean.Contracts.Prompts;

public sealed record PromptStageDto(PromptStage Id, string Name, string When, IReadOnlyList<PromptDto> Prompts);

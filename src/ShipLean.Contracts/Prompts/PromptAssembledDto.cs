namespace ShipLean.Contracts.Prompts;

// Text is the prompt exactly as that agent gets it, placeholders unfilled.
public sealed record PromptAssembledDto(PromptKey Key, string Agent, IReadOnlyList<PromptLayerDto> Layers, string Text);

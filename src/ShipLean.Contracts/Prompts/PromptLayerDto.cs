namespace ShipLean.Contracts.Prompts;

public sealed record PromptLayerDto(PromptLayerKind Kind, string Title, string? Text, bool Planned, string? Note);

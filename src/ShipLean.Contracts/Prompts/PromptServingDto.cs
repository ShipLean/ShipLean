namespace ShipLean.Contracts.Prompts;

// Label reads "brief.md custom v2 · Claude Code" or "guide.md built-in · Another agent".
public sealed record PromptServingDto(PromptKey Key, string File, int? Version, string Agent, string Label, string AtLabel);

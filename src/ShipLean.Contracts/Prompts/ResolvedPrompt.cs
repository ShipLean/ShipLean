namespace ShipLean.Contracts.Prompts;

public sealed record ResolvedPrompt(PromptKey Key, string Text, int? Version, string Agent, string BuiltInHash);

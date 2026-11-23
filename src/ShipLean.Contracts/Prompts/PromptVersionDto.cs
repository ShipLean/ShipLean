namespace ShipLean.Contracts.Prompts;

// Agent is the stamp of the agent that saved the version over MCP ("Claude Code (claude.ai)"), null for a web save.
public sealed record PromptVersionDto(int Number, string Note, string By, string AtLabel, bool InUse, PromptVersionSource Source, string Text, string? Agent);

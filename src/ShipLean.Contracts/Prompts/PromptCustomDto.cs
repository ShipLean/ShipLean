namespace ShipLean.Contracts.Prompts;

// BaseText is the built-in this version started from, for the editor's live diff.
public sealed record PromptCustomDto(int Number, string Text, bool InUse, string By, string AtLabel, string Note, string BaseLabel, string BaseText);

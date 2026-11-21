namespace ShipLean.Application.Features.Prompts.Reads;

// One prompt_version row with its base built-in and author, as the reads need it.
internal sealed record PromptVersionRow(
    string Prompt, Guid Id, int Number, string Text, string BaseHash, string BaseText, DateTime BaseFirstSeen, string Note, string Source,
    string? By, DateTime CreatedAt);

namespace ShipLean.Contracts.Prompts;

// Patch is PromptDiff.Patch from the text in use (the built-in when no version is in use) to the new text; Bytes is the new text's UTF-8 size.
public sealed record PromptSavePreviewDto(PromptKey Key, int? InUseVersion, int NextVersion, string Patch, int Bytes, IReadOnlyList<PromptWarningDto> Warnings);

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// What save_prompt's preview seals into its plan (C4): the prompt's wire key, sha256 of the text and of the note, and based_on (null = built-in).
// The apply call resends the arguments; any difference from these is refused.
internal sealed record SavePromptPlan(string Prompt, string TextHash, string NoteHash, int? BasedOn);

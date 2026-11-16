namespace ShipLean.Contracts.Inspection;

// Prompt is null when the project hides content (PromptHidden then says so) or the turn has no prompt text.
public sealed record RunPageTurnDto(int Number, string Label, string? Prompt, bool PromptHidden, IReadOnlyList<RunPageRowDto> Rows);

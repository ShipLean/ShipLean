namespace ShipLean.Application.Features.Prompts.Updates;

/// <summary>One step of an edit script: Kind is ' ' (kept), '-' (only in the old text) or '+' (only in the new text); A and B are the 0-based line positions the step starts at in the old and new text.</summary>
internal readonly record struct LineEdit(char Kind, int A, int B);

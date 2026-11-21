namespace ShipLean.Application.Features.Prompts.Reads;

internal sealed record PromptPointerRow(string Prompt, Guid? InUseVersionId, string? KeptBuiltInHash);

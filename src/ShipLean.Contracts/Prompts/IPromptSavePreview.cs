namespace ShipLean.Contracts.Prompts;

// C3: what a save would do, without writing. PreviewAsync throws exactly what IPromptWrites.SaveAsync throws for the same input,
// in the same order (note, text, expected, then "Nothing changed"). Warnings is pure: the apply result returns it, computed before the write.
public interface IPromptSavePreview
{
    Task<PromptSavePreviewDto> PreviewAsync(Guid projectId, PromptKey key, string text, string note, PromptBase expected, CancellationToken cancellationToken = default);

    IReadOnlyList<PromptWarningDto> Warnings(PromptKey key, string text);
}

using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

// C3: what save_prompt would write, without writing. The note and text rules are already the save's (PromptTextRules).
public sealed class DbPromptSavePreview : IPromptSavePreview
{
    public Task<PromptSavePreviewDto> PreviewAsync(Guid projectId, PromptKey key, string text, string note, PromptBase expected, CancellationToken cancellationToken = default)
    {
        PromptTextRules.Note(note);
        PromptTextRules.Text(text);
        // TODO: real checks, diff and warnings
        return Task.FromResult(new PromptSavePreviewDto(key, null, 1, string.Empty, 0, []));
    }

    // TODO: real checks, diff and warnings
    public IReadOnlyList<PromptWarningDto> Warnings(PromptKey key, string text) => [];
}

using System.Text;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Prompts.Writes;

// The note and text rules a save checks, shared by DbPromptWrites and DbPromptSavePreview so both throw the same errors (C2, C3).
internal static class PromptTextRules
{
    private const int MaxTextBytes = 65_536;
    private const int MaxNoteLength = 200;

    // The trimmed note.
    public static string Note(string? note)
    {
        var trimmed = note?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw ApiException.Validation("Add a note saying what you changed.", "note");
        }

        if (trimmed.Length > MaxNoteLength)
        {
            throw ApiException.Validation($"The note is at most {MaxNoteLength} characters.", "note");
        }

        return trimmed;
    }

    // The text with \n line ends.
    public static string Text(string? text)
    {
        var normalised = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(normalised))
        {
            throw ApiException.Validation("The prompt text can't be empty.", "text");
        }

        if (Encoding.UTF8.GetByteCount(normalised) > MaxTextBytes)
        {
            throw ApiException.Validation("The prompt text is at most 64 KB.", "text");
        }

        var error = AgentSections.Check(normalised).FirstOrDefault(problem => problem.Kind != AgentSectionProblemKind.UnknownTag);
        if (error is not null)
        {
            var what = error.Kind switch
            {
                AgentSectionProblemKind.Unclosed => $"<{error.Tag}> is never closed",
                AgentSectionProblemKind.Nested => $"<{error.Tag}> is opened inside another agent section",
                _ => $"</{error.Tag}> has no matching opening tag",
            };
            throw ApiException.Validation($"Line {error.Line}: {what}.", "text");
        }

        return normalised;
    }
}

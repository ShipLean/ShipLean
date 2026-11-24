using System.Text;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

// The rules a save checks, shared by DbPromptWrites and DbPromptSavePreview so both throw the same errors in the same order (C2, C3):
// Note, Text, Expected, then Changed.
internal static class PromptTextRules
{
    private const int MaxTextBytes = 65_536;
    private const int MaxNoteLength = 200;

    // The trimmed note. It is optional (D20): a missing or blank note is stored as "".
    public static string Note(string? note)
    {
        var trimmed = note?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxNoteLength)
        {
            throw ApiException.Validation($"The note is at most {MaxNoteLength} characters.", "note");
        }

        return trimmed;
    }

    // The text with \n line ends.
    public static string Text(string? text)
    {
        var normalised = Normalised(text ?? string.Empty);
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

    // The version the caller read as in use (null expected: no check) against the version in use now (null: the built-in).
    public static void Expected(PromptBase? expected, int? inUse)
    {
        if (expected is not null && expected.Version != inUse)
        {
            var now = inUse is { } number ? $"v{number}" : "the built-in";
            throw ApiException.Conflict($"The version in use changed since you read it: it is now {now}. Read the prompt again with prompts, then save.");
        }
    }

    // The normalised new text against the text in use now (the built-in, normalised, when no version is in use).
    public static void Changed(string text, string inUse)
    {
        if (text == inUse)
        {
            throw ApiException.Validation("Nothing changed from the version in use.", "text");
        }
    }

    // The built-in text with \n line ends, as a save compares and stores it.
    public static string Normalised(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}

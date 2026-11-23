using System.Text.RegularExpressions;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

// The checks that don't stop a save, ported from the editor (placeholders.ts, promptChecks.ts) with its wording: placeholders the prompt
// expects that the text dropped (in the facts' order), {{name}} tokens ShipLean doesn't fill in (first-seen order), then agent tags it
// doesn't recognise (with their line). Section errors are not here: they stop the save (PromptTextRules.Text).
internal static partial class PromptWarnings
{
    public static IReadOnlyList<PromptWarningDto> Of(string text, IReadOnlyList<string> placeholders)
    {
        var normalised = PromptTextRules.Normalised(text);
        var used = Token().Matches(normalised).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
        var missing = placeholders.Where(placeholder => !used.Contains(placeholder, StringComparer.Ordinal))
            .Select(placeholder => new PromptWarningDto(
                PromptWarningKind.PlaceholderMissing, $"{placeholder} is missing, so agents won't get that part.", placeholder, null, null));
        var unknown = used.Where(token => !placeholders.Contains(token, StringComparer.Ordinal))
            .Select(token => new PromptWarningDto(
                PromptWarningKind.PlaceholderUnknown, $"{token} isn't one ShipLean fills in for this prompt; it stays as typed.", token, null, null));
        var tags = AgentSections.Check(normalised).Where(problem => problem.Kind == AgentSectionProblemKind.UnknownTag)
            .Select(problem => new PromptWarningDto(
                PromptWarningKind.UnknownTag, $"<{problem.Tag}> isn't an agent ShipLean recognises, so every agent gets it as typed.", null, problem.Tag, problem.Line));
        return [.. missing, .. unknown, .. tags];
    }

    [GeneratedRegex(@"\{\{[A-Za-z0-9_]+\}\}")]
    private static partial Regex Token();
}

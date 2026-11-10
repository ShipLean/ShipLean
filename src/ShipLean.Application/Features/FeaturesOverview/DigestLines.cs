using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.Core;

namespace ShipLean.Application.Features.FeaturesOverview;

// Turns one change (a revision or a comment) into the digest lines it deserves, text made here so clients render it as it comes.
// A revision gives a line for each of state, health and the status line it changed ("Active → Closed", "On track → Blocked", the
// new status line). A comment by a person gives its body as plain text, cut on a word at 280 characters. Anything an agent did
// that gave none of those gives one agentWork line ("Claude Code commented", "Claude Code changed Story points"). Other changes
// by people give nothing.
public static partial class DigestLines
{
    public const int CommentLength = 280;

    private static readonly string[] DigestFields = [FieldIds.State, FieldIds.Health, FieldIds.StatusLine];

    public static IReadOnlyList<DigestLineDto> Of(ChangeEntryDto entry, UserRef? by, TimeProvider clock)
    {
        var atLabel = DisplayDate.Relative(entry.At, clock);
        DigestLineDto Line(DigestLineKind kind, string text) => new(kind, text, by, entry.AgentName, entry.At, atLabel);

        if (entry.Kind == ChangeKind.Comment)
        {
            if (entry.AgentName is { } commenter)
            {
                return [Line(DigestLineKind.AgentWork, $"{commenter} commented")];
            }

            var body = CommentText(entry.Comment);
            return body.Length == 0 ? [] : [Line(DigestLineKind.Comment, body)];
        }

        var fields = entry.Fields ?? [];
        var lines = new List<DigestLineDto>();
        foreach (var change in fields.Where(f => DigestFields.Contains(f.Field)))
        {
            switch (change.Field)
            {
                case FieldIds.State:
                    lines.Add(Line(DigestLineKind.State, Transition(change)));
                    break;
                case FieldIds.Health:
                    lines.Add(Line(DigestLineKind.Health, Transition(change)));
                    break;
                default:
                    if (Text(change.To, change.ToLabel, null) is { Length: > 0 } statusLine)
                    {
                        lines.Add(Line(DigestLineKind.StatusLine, statusLine));
                    }

                    break;
            }
        }

        if (lines.Count == 0 && entry.AgentName is { } agent && fields.Count > 0)
        {
            var labels = fields.Select(f => FieldIds.All.FirstOrDefault(d => d.Id == f.Field)?.Label ?? f.Field).Distinct();
            lines.Add(Line(DigestLineKind.AgentWork, $"{agent} changed {string.Join(", ", labels)}"));
        }

        return lines;
    }

    // The plain text of a markdown comment on one line, cut on a word when it is longer than CommentLength.
    public static string CommentText(string? markdown)
    {
        var plain = Whitespace().Replace(Markdig.Markdown.ToPlainText(markdown ?? string.Empty), " ").Trim();
        if (plain.Length <= CommentLength)
        {
            return plain;
        }

        var cut = plain[..CommentLength];
        var space = cut.LastIndexOf(' ');
        return $"{(space > 0 ? cut[..space] : cut).TrimEnd()}…";
    }

    // "Active → Closed", or just the new value when there was no old one.
    private static string Transition(FieldChange change)
    {
        var options = FieldIds.All.FirstOrDefault(d => d.Id == change.Field)?.Values;
        var to = Text(change.To, change.ToLabel, options);
        return Text(change.From, change.FromLabel, options) is { Length: > 0 } from ? $"{from} → {to}" : to;
    }

    private static string Text(JsonElement value, string? label, IReadOnlyList<FieldValueOption>? options)
    {
        if (label is not null)
        {
            return label;
        }

        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return string.Empty;
        }

        var option = options?.FirstOrDefault(o => o.Value.ValueKind == value.ValueKind && o.Value.GetRawText() == value.GetRawText());
        return option?.Label ?? (value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

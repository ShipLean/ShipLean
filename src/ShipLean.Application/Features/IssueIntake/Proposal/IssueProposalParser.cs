using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Proposal;

// Parses a proposal agent's response (C2): public Markdown, then one final ```shiplean-proposal block holding the JSON.
// It never throws: a response it can't use fails with a reason. Text past the limits is cut to them, not refused.
public static partial class IssueProposalParser
{
    public const string BlockTag = "shiplean-proposal";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().Build();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static bool TryParse(string response, [NotNullWhen(true)] out IssueProposal? proposal, out string publicMarkdown) =>
        TryParse(response, out proposal, out publicMarkdown, out _);

    // failure says why the response was refused, for the job's last_error; it is null on success.
    public static bool TryParse(
        string response, [NotNullWhen(true)] out IssueProposal? proposal, out string publicMarkdown, out string? failure)
    {
        proposal = null;
        publicMarkdown = string.Empty;
        failure = FindBlock(response, out var block);
        if (block is null)
        {
            return false;
        }

        publicMarkdown = response[..block.Span.Start].TrimEnd();
        failure = Read(block.Lines.ToString(), out proposal);
        return proposal is not null;
    }

    private static string? FindBlock(string response, out FencedCodeBlock? block)
    {
        block = null;
        MarkdownDocument document;
        try
        {
            document = Markdig.Markdown.Parse(response, Pipeline);
        }
        catch (ArgumentException)
        {
            return "The response is nested too deeply to read.";
        }

        var blocks = document.Descendants<FencedCodeBlock>().Where(b => string.Equals(b.Info, BlockTag, StringComparison.Ordinal)).ToList();
        if (blocks.Count != 1)
        {
            return blocks.Count == 0 ? $"The response has no {BlockTag} block." : $"The response has {blocks.Count} {BlockTag} blocks; it needs one.";
        }

        block = blocks[0];
        return null;
    }

    private static string? Read(string json, out IssueProposal? proposal)
    {
        proposal = null;
        ProposalJson? raw;
        try
        {
            raw = JsonSerializer.Deserialize<ProposalJson>(json, Json);
        }
        catch (JsonException e)
        {
            return $"The {BlockTag} block is not valid JSON: {e.Message}";
        }

        var failure = Check(raw);
        if (failure is not null)
        {
            return failure;
        }

        proposal = new IssueProposal(
            raw!.Verdict!.Value,
            raw.Type!.Value,
            Cut(raw.Title!.Trim(), IssueProposal.MaxTitle),
            raw.Description is null ? null : Cut(raw.Description, IssueProposal.MaxDescription),
            Take(raw.AcceptanceCriteria, IssueProposal.MaxAcceptanceCriteria),
            raw.ParentKey,
            (raw.Existing ?? []).Take(IssueProposal.MaxExisting).ToList(),
            Take(raw.Requires, IssueProposal.MaxRequires),
            Take(raw.Questions, IssueProposal.MaxQuestions));
        return null;
    }

    private static string? Check(ProposalJson? raw)
    {
        if (raw?.Verdict is null || raw.Type is null)
        {
            return "The proposal needs a verdict (new, alreadyShipped, duplicate or needsInfo) and a type (feature, story or bug).";
        }

        if (string.IsNullOrWhiteSpace(raw.Title))
        {
            return "The proposal needs a title.";
        }

        if (raw.Verdict == ProposalVerdict.New && string.IsNullOrWhiteSpace(raw.Description))
        {
            return "A new proposal needs a description.";
        }

        if (raw.ParentKey is not null && !KeyPattern().IsMatch(raw.ParentKey))
        {
            return $"parentKey {raw.ParentKey} is not a work item key.";
        }

        var existing = raw.Existing ?? [];
        if (existing.Any(e => e is null))
        {
            return "existing has an empty entry.";
        }

        var badKey = existing.FirstOrDefault(e => e.Key is not null && !KeyPattern().IsMatch(e.Key));
        if (badKey is not null)
        {
            return $"existing key {badKey.Key} is not a work item key.";
        }

        var badCommit = existing.FirstOrDefault(e => e.Commit is not null && !CommitPattern().IsMatch(e.Commit));
        return badCommit is null ? null : $"existing commit {badCommit.Commit} is not 7 to 40 hex characters.";
    }

    private static string Cut(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var keep = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..keep];
    }

    private static List<string> Take(IReadOnlyList<string?>? list, int max) =>
        (list ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Take(max).Select(s => s!).ToList();

    [GeneratedRegex(@"^[A-Z]{2,4}-[0-9]+$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[0-9a-fA-F]{7,40}$")]
    private static partial Regex CommitPattern();

    private sealed record ProposalJson(
        ProposalVerdict? Verdict,
        ProposalType? Type,
        string? Title,
        string? Description,
        IReadOnlyList<string?>? AcceptanceCriteria,
        string? ParentKey,
        IReadOnlyList<ProposalExisting>? Existing,
        IReadOnlyList<string?>? Requires,
        IReadOnlyList<string?>? Questions);
}

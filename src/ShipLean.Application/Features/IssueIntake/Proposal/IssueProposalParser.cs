using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Proposal;

// Parses a proposal agent's response (C2): public Markdown, then one final ```shiplean-proposal block holding the JSON.
// It never throws: a response it can't use fails with a reason, and so does any field past its limit (nothing is cut).
public static partial class IssueProposalParser
{
    public const string BlockTag = "shiplean-proposal";

    // The whole response, in UTF-8 bytes, checked before any parsing.
    public const int MaxResponseBytes = 256 * 1024;

    // Each acceptance criterion, requirement and question.
    public const int MaxEntry = 1_000;

    public const int MaxUrl = 2_048;

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
        var bytes = Encoding.UTF8.GetByteCount(response);
        if (bytes > MaxResponseBytes)
        {
            failure = $"The response is {bytes} bytes, past the {MaxResponseBytes} limit.";
            return false;
        }

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

        var failure = Check(raw) ?? CheckLimits(raw!);
        if (failure is not null)
        {
            return failure;
        }

        proposal = new IssueProposal(
            raw!.Verdict!.Value,
            raw.Type!.Value,
            raw.Title!.Trim(),
            raw.Description,
            NonBlank(raw.AcceptanceCriteria),
            raw.ParentKey,
            raw.Existing ?? [],
            NonBlank(raw.Requires),
            NonBlank(raw.Questions));
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

    // Every limit is a refusal, never a cut: a proposal past one goes to a maintainer, not into ShipLean shortened.
    private static string? CheckLimits(ProposalJson raw)
    {
        var title = raw.Title!.Trim();
        if (title.Length > IssueProposal.MaxTitle)
        {
            return $"title is {title.Length} characters, past the {IssueProposal.MaxTitle} limit.";
        }

        if (raw.Description?.Length > IssueProposal.MaxDescription)
        {
            return $"description is {raw.Description.Length} characters, past the {IssueProposal.MaxDescription} limit.";
        }

        return CheckList("acceptanceCriteria", raw.AcceptanceCriteria, IssueProposal.MaxAcceptanceCriteria)
            ?? CheckList("requires", raw.Requires, IssueProposal.MaxRequires)
            ?? CheckList("questions", raw.Questions, IssueProposal.MaxQuestions)
            ?? CheckExisting(raw.Existing ?? []);
    }

    private static string? CheckList(string field, IReadOnlyList<string?>? list, int max)
    {
        var entries = list ?? [];
        if (entries.Count > max)
        {
            return $"{field} has {entries.Count} entries, past the {max} limit.";
        }

        var index = entries.ToList().FindIndex(e => e?.Length > MaxEntry);
        return index < 0 ? null : $"{field}[{index}] is {entries[index]!.Length} characters, past the {MaxEntry} limit.";
    }

    private static string? CheckExisting(IReadOnlyList<ProposalExisting> existing)
    {
        if (existing.Count > IssueProposal.MaxExisting)
        {
            return $"existing has {existing.Count} entries, past the {IssueProposal.MaxExisting} limit.";
        }

        var index = existing.ToList().FindIndex(e => e.PrUrl?.Length > MaxUrl || e.RunUrl?.Length > MaxUrl);
        return index < 0 ? null : $"existing[{index}] has a URL past the {MaxUrl} character limit.";
    }

    private static List<string> NonBlank(IReadOnlyList<string?>? list) =>
        (list ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();

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

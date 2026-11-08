namespace ShipLean.Contracts.IssueIntake;

// A proposal agent's verdict on a GitHub issue (C2), parsed from the final ```shiplean-proposal block of its response by
// IssueProposalParser. Description is set for New (and may be for the others). Existing names the shipped work or the
// duplicate. Requires lists the conditions a shipped feature needs (agent dispatch connected, the webhook secret set, ...).
public sealed record IssueProposal(
    ProposalVerdict Verdict,
    ProposalType Type,
    string Title,
    string? Description,
    IReadOnlyList<string> AcceptanceCriteria,
    string? ParentKey,
    IReadOnlyList<ProposalExisting> Existing,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Questions)
{
    public const int MaxTitle = 255;
    public const int MaxDescription = 64 * 1024;
    public const int MaxAcceptanceCriteria = 50;
    public const int MaxExisting = 10;
    public const int MaxRequires = 10;
    public const int MaxQuestions = 3;
}

// One piece of existing work the proposal points at: a ShipLean item, a PR, the merge commit (7–40 hex), when it merged,
// and when and by which workflow run its image was published.
public sealed record ProposalExisting(
    string? Key,
    string? PrUrl,
    string? Commit,
    DateTimeOffset? MergedAt,
    DateTimeOffset? PublishedAt,
    string? RunUrl);

// The JSON spells these camelCase: new, alreadyShipped, duplicate, needsInfo.
public enum ProposalVerdict
{
    New,
    AlreadyShipped,
    Duplicate,
    NeedsInfo,
}

// The JSON spells these feature, story, bug.
public enum ProposalType
{
    Feature,
    Story,
    Bug,
}

using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.ShippedIn;

// One verified "shipped in" fact (D14): an item of the project, one of its merged github.com pull requests with the merge commit
// and merge time ShipLean stored from the pull_request webhook (link_state), and what IImagePublications says about that commit.
// Nothing here comes from an agent.
public sealed record ShippedFact(
    string Key, string Repository, int PullNumber, string MergeCommitSha, DateTimeOffset MergedAt, ImagePublication Publication)
{
    public string ShortSha => MergeCommitSha[..7];
}

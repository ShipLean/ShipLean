namespace ShipLean.Contracts.IssueIntake;

// One GitHub issue: the host ("github.com"), the repository as "owner/name", and the issue number.
public sealed record IssueRef(string Host, string Repository, int Number)
{
    public const string GitHubHost = "github.com";

    public override string ToString() => $"{Repository}#{Number}";
}

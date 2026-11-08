namespace ShipLean.Application.Features.IssueIntake.Proposal;

// The last check (D17) on every agent-written text before it is posted on GitHub. The caller builds one per post with the
// instance's base URLs and the names of the project's members, then calls Clean with the issue author's login.
public sealed class PublicTextGuard(IReadOnlyCollection<string> instanceBaseUrls, IReadOnlyCollection<string> memberNames)
{
    public const int MaxLength = 60_000;

    public GuardResult Clean(string markdown, string issueAuthor)
    {
        // TODO: the guard replaces this.
        throw new NotSupportedException($"The guard is not built yet ({instanceBaseUrls.Count}, {memberNames.Count}, {markdown.Length}, {issueAuthor}).");
    }
}

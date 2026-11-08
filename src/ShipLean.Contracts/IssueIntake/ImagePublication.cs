namespace ShipLean.Contracts.IssueIntake;

// The answer of IImagePublications. Published: the run finished at At (RunSha is the commit it built, the fix or a later one).
// Failed: the run for that commit failed. Pending: no finished run yet. NotApplicable: the repository has no publish workflow,
// so a reply gives the commit only. Unknown: GitHub could not be asked.
public abstract record ImagePublication
{
    private ImagePublication()
    {
    }

    public sealed record Published(DateTimeOffset At, string RunUrl, string RunSha) : ImagePublication;

    public sealed record Failed(string RunUrl) : ImagePublication;

    public sealed record Pending : ImagePublication;

    public sealed record NotApplicable : ImagePublication;

    public sealed record Unknown : ImagePublication;
}

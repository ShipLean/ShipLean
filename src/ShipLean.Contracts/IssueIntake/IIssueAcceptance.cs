namespace ShipLean.Contracts.IssueIntake;

// The acceptance lane: an LGTM from the issue author or a maintainer (D19). Idempotent: a second LGTM, or the same one
// delivered twice, does nothing more.
public interface IIssueAcceptance
{
    Task<IssueOutcome> AcceptAsync(IssueCommentEvent comment, CancellationToken cancellationToken);
}

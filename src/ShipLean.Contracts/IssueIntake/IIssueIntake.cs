namespace ShipLean.Contracts.IssueIntake;

// The intake lane. OpenedAsync takes a new issue in a repository a project takes issues from (a Triage item and a round);
// ChangedAsync applies an edit, close or reopen to an issue already taken; TakeAsync is a maintainer's "/shiplean", which
// takes an issue not yet taken or restarts a held one. Each is idempotent (D18): the same event twice changes nothing more.
// An issue in a repository no project takes is Ignored.
public interface IIssueIntake
{
    Task<IssueOutcome> OpenedAsync(IssueEvent issue, CancellationToken cancellationToken);

    Task<IssueOutcome> ChangedAsync(IssueEvent issue, CancellationToken cancellationToken);

    Task<IssueOutcome> TakeAsync(IssueCommentEvent comment, CancellationToken cancellationToken);
}

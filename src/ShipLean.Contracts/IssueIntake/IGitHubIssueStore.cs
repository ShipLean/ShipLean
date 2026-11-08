using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// The github_issue rows. Every method joins the caller's transaction when given one, and opens its own connection otherwise.
public interface IGitHubIssueStore
{
    Task<GitHubIssueRecord?> FindAsync(IssueRef issue, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    Task<GitHubIssueRecord?> FindByIdAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    Task<GitHubIssueRecord?> FindByWorkItemAsync(Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Idempotent: when the issue is already stored, returns the stored record unchanged (whatever the arguments say).
    // title and body are the issue's text from GitHub (the caller caps it); UpdateTextAsync replaces them when the issue is edited.
    Task<GitHubIssueRecord> InsertAsync(
        IssueRef issue, Guid projectId, Guid workItemId, string authorLogin, string title, string body, IssuePhase phase, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Replaces the issue's stored title and body (an `edited` issue event) only when eventUpdatedAt (GitHub's issue.updated_at)
    // is newer than the last text change: strictly newer, so on a tie the first delivery wins. False when the event is stale.
    Task<bool> UpdateTextAsync(Guid id, string title, string body, DateTimeOffset eventUpdatedAt, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Compare and set: moves to `to` only when the current phase is one of `from`. False when it isn't (or the id is unknown).
    Task<bool> SetPhaseAsync(Guid id, IReadOnlyList<IssuePhase> from, IssuePhase to, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Replaces the latest proposal: its JSON and the session that wrote it.
    Task SaveProposalAsync(Guid id, Guid sessionId, string proposalJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Adds one round atomically and returns the new count.
    Task<int> IncrementRoundsAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Points the issue at another item (the item an acceptance created, or the existing item of a duplicate).
    Task RepointAsync(Guid id, Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Sets whether the issue is open on GitHub, with the same strictly-newer rule on the last state change. False when stale.
    Task<bool> SetOpenOnGitHubAsync(Guid id, bool open, DateTimeOffset eventUpdatedAt, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Accepted issues still open on GitHub, oldest first: what the progress reconcile loop walks.
    Task<IReadOnlyList<GitHubIssueRecord>> ListAcceptedOpenAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Sets rerun_requested: a reply came in after the pending round's session had already read the comments.
    Task RequestRerunAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Reads and clears rerun_requested in one statement: true for exactly one caller per request.
    Task<bool> TakeRerunAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Records that `milestone` was posted for `value` (LastPosted[milestone] = value), keeping the other milestones.
    Task MarkPostedAsync(Guid id, string milestone, string value, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}

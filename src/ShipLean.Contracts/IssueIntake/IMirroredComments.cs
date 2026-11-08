using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Comments mirrored from a GitHub issue onto its work item, with their provenance (github_issue_comment). A propose session reads
// only comments that have a provenance row: an agent name of "GitHub @login" alone proves nothing (D10, D17).
public interface IMirroredComments
{
    // The ShipLean comment mirrored from the GitHub comment at htmlUrl, or null when it has not been mirrored.
    Task<Guid?> FindByUrlAsync(string htmlUrl, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Writes the comment on the issue's work item through ICommentStore.AddAsAgentAsync (agent name "GitHub @login", written as
    // RunAsUserId) and its provenance row (with the GitHub text, Body, which a propose session reads), both in the caller's
    // transaction. Idempotent per HtmlUrl: it first takes pg_advisory_xact_lock(hashtextextended('github_issue_comment:' || html_url, 0))
    // in the caller's transaction, so two deliveries of one GitHub comment serialise, and the second gets the first one's comment id
    // back with no event. Publishes nothing: after the caller commits, it publishes the result's Changed through IProjectEvents.
    Task<MirroredCommentAdd> AddAsync(MirroredComment comment, DbTransaction transaction, CancellationToken cancellationToken = default);
}

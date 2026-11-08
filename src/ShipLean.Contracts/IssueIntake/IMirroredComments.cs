using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Comments mirrored from a GitHub issue onto its work item, with their provenance (github_issue_comment). A propose session reads
// only comments that have a provenance row: an agent name of "GitHub @login" alone proves nothing (D10, D17).
public interface IMirroredComments
{
    // The ShipLean comment mirrored from the GitHub comment at htmlUrl, or null when it has not been mirrored.
    Task<Guid?> FindByUrlAsync(string htmlUrl, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    // Writes the comment on the issue's work item through ICommentStore.AddAsAgentAsync (agent name "GitHub @login", written as
    // RunAsUserId) and its provenance row, both in the caller's transaction. Returns the ShipLean comment's id. Not idempotent on
    // its own: a second comment with the same HtmlUrl fails on the unique index, so call FindByUrlAsync first in the same transaction.
    Task<Guid> AddAsync(MirroredComment comment, DbTransaction transaction, CancellationToken cancellationToken = default);
}

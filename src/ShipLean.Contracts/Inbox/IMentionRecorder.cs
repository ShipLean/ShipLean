using System.Data.Common;

namespace ShipLean.Contracts.Inbox;

// Records the @mentions in a comment, on the comment's own transaction. The comment store calls RecordAsync when a comment is
// added (AddAsync and both AddAsAgentAsync overloads) and ReconcileAsync when one is edited; ImportAsync and comments mirrored
// from GitHub record none. authorUserId is the person the comment is by (a person never mentions themselves), and agentName the
// agent that wrote it for them, or null. Reconcile records new names and withdraws open mentions whose name was removed.
public interface IMentionRecorder
{
    Task RecordAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string body, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default);

    Task ReconcileAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string newBody, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default);
}

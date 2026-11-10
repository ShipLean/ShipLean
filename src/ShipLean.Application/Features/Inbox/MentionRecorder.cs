using System.Data.Common;
using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

public sealed class MentionRecorder : IMentionRecorder
{
    // TODO: store a comment_mention row for each member MentionText.Find returns; until then nothing is recorded.
    public Task RecordAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string body, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // TODO: record new mentions and withdraw open ones whose name was removed; until then nothing changes.
    public Task ReconcileAsync(
        DbTransaction transaction, Guid commentId, Guid workItemId, string newBody, Guid? authorUserId, string? agentName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

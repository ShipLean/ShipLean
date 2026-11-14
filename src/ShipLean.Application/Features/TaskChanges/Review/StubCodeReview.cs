using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Review;

// TODO: replaced by the review command: head check, state move through the work item store, review revision, sent_back_at, note comment.
public sealed class StubCodeReview : ICodeReview
{
    public Task<ReviewOutcome> ApproveAsync(Guid workItemId, ApproveRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new ReviewOutcome("Closed", 0));

    public Task<ReviewOutcome> RequestChangesAsync(Guid workItemId, RequestChangesRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new ReviewOutcome(request.KeepAssignee ? "Active" : "New", 0));
}

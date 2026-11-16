using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What Approve and Request changes do: the access check first, then the review.
[RegisterPerRequest]
public sealed class ReviewEndpoints(IProjectAccess access, ICodeReview review)
{
    public async Task<ReviewOutcomeDto> ApproveAsync(Guid workItemId, ApproveChangesRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        var outcome = await review.ApproveAsync(workItemId, new ApproveRequest(request.Note, request.HeadCommitId), cancellationToken);
        return new ReviewOutcomeDto(outcome.State, outcome.OpenComments);
    }

    public async Task<ReviewOutcomeDto> RequestAsync(Guid workItemId, RequestTaskChangesRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        var outcome = await review.RequestChangesAsync(workItemId, new RequestChangesRequest(request.Note, request.KeepAssignee), cancellationToken);
        return new ReviewOutcomeDto(outcome.State, outcome.OpenComments);
    }
}

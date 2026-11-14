using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What Approve and Request changes do: the access check first, then the review.
[RegisterPerRequest]
public sealed class ReviewEndpoints(IProjectAccess access)
{
    public async Task<ReviewOutcomeDto> ApproveAsync(Guid workItemId, ApproveChangesRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: approve through ICodeReview.ApproveAsync, which refuses a head commit that is no longer the newest.
        return new ReviewOutcomeDto("Closed", 0);
    }

    public async Task<ReviewOutcomeDto> RequestAsync(Guid workItemId, RequestTaskChangesRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: send the changes back through ICodeReview.RequestChangesAsync.
        return new ReviewOutcomeDto(request.KeepAssignee ? "Active" : "New", 0);
    }
}

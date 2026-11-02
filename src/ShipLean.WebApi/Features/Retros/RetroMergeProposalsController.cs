using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

// A person's answer to an agent's proposal to merge two themes.
[Route("api/retros/theme-merge-proposals")]
public sealed class RetroMergeProposalsController(IRetroMergeProposals proposals, IProjectAccess access, ICurrentUser user) : ApiControllerBase
{
    [HttpPost("{id:guid}/merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Merge(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroMergeProposal, id, ProjectRole.Member, cancellationToken);
        await proposals.MergeAsync(id, new RetroActor(user.Id, null), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/keep-apart")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> KeepApart(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroMergeProposal, id, ProjectRole.Member, cancellationToken);
        await proposals.KeepApartAsync(id, new RetroActor(user.Id, null), cancellationToken);
        return NoContent();
    }
}

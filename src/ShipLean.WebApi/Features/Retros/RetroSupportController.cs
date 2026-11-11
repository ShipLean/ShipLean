using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Retros.Support;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/retros/points/{id:guid}/support")]
public sealed class RetroSupportController(IRetroSupport support, IProjectAccess access) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Support(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        await support.SupportAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        await support.WithdrawAsync(id, cancellationToken);
        return NoContent();
    }
}

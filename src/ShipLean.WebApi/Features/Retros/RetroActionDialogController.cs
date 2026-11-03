using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/projects/{id:guid}/retros/action-dialog")]
public sealed class RetroActionDialogController(IRetroActionDialogReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<RetroActionDialogDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroActionDialogDto> Get(Guid id, [FromQuery] Guid[] point, [FromQuery] Guid? theme, [FromQuery] Guid? item, [FromQuery] Guid? sprint, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reader.GetAsync(id, point, theme, item, sprint, cancellationToken);
    }
}

using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/projects/{id:guid}/retros/themes")]
public sealed class RetroThemesController(IRetroThemeReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<RetroThemeListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroThemeListDto> List(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reader.ListAsync(id, cancellationToken);
    }
}

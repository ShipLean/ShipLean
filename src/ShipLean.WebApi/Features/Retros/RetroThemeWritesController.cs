using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api")]
public sealed class RetroThemeWritesController(IRetroPeopleWrites writes, IProjectAccess access) : ApiControllerBase
{
    [HttpPut("retros/themes/{id:guid}")]
    [ProducesResponseType<RetroThemeRefDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroThemeRefDto> Rename(Guid id, RetroThemeRename request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroTheme, id, ProjectRole.Member, cancellationToken);
        RetroChecks.Rename(request);
        return await writes.RenameThemeAsync(id, request, cancellationToken);
    }

    [HttpPost("projects/{id:guid}/retros/themes/merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Merge(Guid id, RetroThemeMerge request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        RetroChecks.Merge(request);
        return await NoContentAfter(writes.MergeThemesAsync(id, request, cancellationToken));
    }
}

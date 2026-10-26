using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Boards.Settings;

[Route("api/boards/{id:guid}/settings")]
public sealed class BoardSettingsController(IBoardSettingsStore store, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BoardSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<BoardSettingsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Board, id, ProjectRole.Member, cancellationToken);
        return await store.GetAsync(id, cancellationToken);
    }

    [HttpPut]
    [ProducesResponseType<BoardSettingsPutResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<BoardSettingsPutResult> Put(Guid id, BoardSettingsPut put, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Board, id, ProjectRole.Member, cancellationToken);
        return await store.PutAsync(id, put, cancellationToken);
    }
}

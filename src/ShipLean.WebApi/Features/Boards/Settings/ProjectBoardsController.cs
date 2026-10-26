using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Boards.Settings;

[Route("api/projects/{id:guid}/boards")]
public sealed class ProjectBoardsController(IBoardSettingsStore store, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BoardSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<BoardSummaryDto>> List(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await store.ListAsync(id, cancellationToken);
    }
}

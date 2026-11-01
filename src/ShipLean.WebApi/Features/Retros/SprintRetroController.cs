using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api/sprints/{id:guid}/retro")]
public sealed class SprintRetroController(ISprintRetroReader reader, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SprintRetroDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintRetroDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await reader.GetAsync(id, cancellationToken);
    }
}

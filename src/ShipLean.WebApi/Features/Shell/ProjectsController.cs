using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Shell;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Shell;

[Route("api/projects")]
public sealed class ProjectsController(ShellReads reads, IProjectAccess access) : ApiControllerBase
{
    // The caller's own live memberships, so it needs no project access check.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProjectSummaryDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ProjectSummaryDto>> List(CancellationToken cancellationToken) => reads.ProjectsAsync(cancellationToken);

    [HttpGet("by-key/{key}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ProjectDto> ByKey(string key, CancellationToken cancellationToken) => reads.ByKeyAsync(key, access, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.ProjectAsync(id, cancellationToken);
    }

    [HttpGet("{id:guid}/sprints")]
    [ProducesResponseType<IReadOnlyList<SprintDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<SprintDto>> Sprints(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.SprintsAsync(id, cancellationToken);
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType<IReadOnlyList<MemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<MemberDto>> Members(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.MembersAsync(id, cancellationToken);
    }

    [HttpGet("{id:guid}/states")]
    [ProducesResponseType<IReadOnlyList<StateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<StateDto>> States(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await reads.StatesAsync(id, cancellationToken);
    }
}

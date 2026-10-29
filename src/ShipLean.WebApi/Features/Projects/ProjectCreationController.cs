using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Projects;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Projects;

// Not project-scoped: any signed-in user may ask, subject to the instance's project_creation rule (the factory) and, for a copy, membership of the source (the gate).
[Route("api/projects")]
public sealed class ProjectCreationController(IProjectFactory factory, ProjectCreationGate gate, FirstSprintDefaults firstSprint) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ProjectDto> Create(ProjectCreate create, CancellationToken cancellationToken)
    {
        await gate.RequireAsync(create, cancellationToken);
        return await factory.CreateAsync(create, cancellationToken);
    }

    [HttpGet("key-check")]
    [ProducesResponseType<KeyCheckDto>(StatusCodes.Status200OK)]
    public Task<KeyCheckDto> KeyCheck([FromQuery] string? key, [FromQuery] string? name, CancellationToken cancellationToken)
        => factory.CheckKeyAsync(key ?? string.Empty, name ?? string.Empty, cancellationToken);

    [HttpGet("first-sprint")]
    [ProducesResponseType<SprintCreate>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public SprintCreate FirstSprint([FromQuery] string? workingDays, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end) => firstSprint.Get(workingDays, start, end);
}

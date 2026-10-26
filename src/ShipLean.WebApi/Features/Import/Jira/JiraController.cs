using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Import.Jira;

// The Jira half of the import stepper: the connection a project's admins saved, a test of it, and the Jira projects it can see.
[Route("api/projects/{id:guid}/jira")]
public sealed class JiraController(JiraConnectionService connections, IProjectAccess access) : ApiControllerBase
{
    // The current connection, or 204 when none is saved. Never the token.
    [HttpGet("connection")]
    [ProducesResponseType<JiraConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JiraConnectionDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.GetAsync(id, cancellationToken) is { } connection ? connection : NoContent();
    }

    // Save the connection; saving over an existing one replaces it (the old row is archived).
    [HttpPut("connection")]
    [ProducesResponseType<JiraConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<JiraConnectionDto> Put(Guid id, JiraConnectionPut put, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.SaveAsync(id, put, cancellationToken);
    }

    [HttpPost("connection/test")]
    [ProducesResponseType<JiraTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<JiraTestResultDto> Test(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.TestAsync(id, cancellationToken);
    }

    [HttpGet("projects")]
    [ProducesResponseType<IReadOnlyList<JiraProjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<JiraProjectDto>> Projects(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.ListProjectsAsync(id, cancellationToken);
    }
}

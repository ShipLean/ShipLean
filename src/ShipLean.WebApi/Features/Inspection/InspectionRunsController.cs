using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inspection;

// K5 reads: not_found while the instance or the project has Inspection off.
[Route("api/inspection/projects/{project}/items/{key}/runs")]
public sealed class InspectionRunsController(InspectionViews views) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AgentRunsSectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentRunsSectionDto> Section(string project, string key, CancellationToken cancellationToken)
        => views.SectionAsync(project, key, cancellationToken);

    [HttpGet("{runId}")]
    [ProducesResponseType<RunPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<RunPageDto> Run(string project, string key, string runId, CancellationToken cancellationToken)
        => views.RunAsync(project, key, runId, cancellationToken);

    [HttpGet("{runId}/rows/{rowId}")]
    [ProducesResponseType<RunRowDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<RunRowDetailDto> Row(string project, string key, string runId, string rowId, CancellationToken cancellationToken)
        => views.RowAsync(project, key, runId, rowId, cancellationToken);
}

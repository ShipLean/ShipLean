using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Backlog.Stories;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Backlog.Stories;

// Writes from the Stories backlog. Bulk: 1 to 100 rows, each with changes, a placement or both; a stale row is not an error, it
// comes back in Stale and the others are written. Items: new rows (and their tasks) placed as one block, all or nothing.
// Duplicates: which typed titles look like an existing story or bug. Over 100 rows: 400 validation.
[Route("api/projects/{id:guid}/backlog")]
public sealed class BacklogWritesController(BacklogWriteCommands commands) : ApiControllerBase
{
    [HttpPost("bulk")]
    [ProducesResponseType<BulkWriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<BulkWriteResult> Bulk(Guid id, BulkWriteRequest request, CancellationToken cancellationToken)
        => commands.BulkAsync(id, request, cancellationToken);

    [HttpPost("items")]
    [ProducesResponseType<BacklogAddResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<BacklogAddResult> Add(Guid id, BacklogAddRequest request, CancellationToken cancellationToken)
        => commands.AddAsync(id, request, cancellationToken);

    [HttpPost("duplicates")]
    [ProducesResponseType<DuplicateCheckResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<DuplicateCheckResult> Duplicates(Guid id, DuplicateCheckRequest request, CancellationToken cancellationToken)
        => commands.DuplicatesAsync(id, request, cancellationToken);
}

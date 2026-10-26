using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Search;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Search;

// The instance admin's search index: one synchronous indexing pass, the same one the background service runs.
// POST /api/admin/search/reindex?full= -> IndexPassResult
[InstanceAdmin]
[Route("api/admin/search")]
public sealed class SearchAdminController(ISearchIndexer indexer) : ApiControllerBase
{
    [HttpPost("reindex")]
    [ProducesResponseType<IndexPassResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IndexPassResult> Reindex([FromQuery] bool full = false, CancellationToken cancellationToken = default)
        => indexer.RunPassAsync(full, cancellationToken);
}

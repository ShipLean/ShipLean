using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Search.Query;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.Search;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Search;

[Route("api/search")]
public sealed class SearchController(SearchViews views) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<SearchViewDto> Get(
        [FromQuery] string? q,
        [FromQuery] string? project,
        [FromQuery] string? types,
        [FromQuery] int limit = SearchRequest.DefaultLimit,
        [FromQuery] bool closed = false,
        [FromQuery] bool prefix = false,
        CancellationToken cancellationToken = default)
        => views.SearchAsync(new SearchRequest(q, project, types, limit, closed, prefix), cancellationToken);
}

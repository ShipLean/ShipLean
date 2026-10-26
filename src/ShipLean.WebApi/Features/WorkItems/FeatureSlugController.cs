using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItems.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItems;

[Route("api/projects/{id:guid}/features")]
public sealed class FeatureSlugController(FeatureSlugCheck slugCheck) : ApiControllerBase
{
    [HttpGet("slug-check")]
    [ProducesResponseType<SlugCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<SlugCheckDto> Check(Guid id, [FromQuery] string? value, CancellationToken cancellationToken)
        => slugCheck.CheckAsync(id, value, cancellationToken);
}

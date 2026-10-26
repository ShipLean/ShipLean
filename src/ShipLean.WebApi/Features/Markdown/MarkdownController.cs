using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Markdown;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Markdown;

[Route("api/markdown")]
public sealed class MarkdownController(MarkdownEndpoints endpoints) : ApiControllerBase
{
    [HttpPost("render")]
    [ProducesResponseType<RenderedMarkdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<RenderedMarkdownDto> Render(MarkdownRenderRequest request, CancellationToken cancellationToken)
        => endpoints.RenderAsync(request, cancellationToken);
}

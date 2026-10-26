using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Links.OpenGraph;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Links;

// The stored preview image and favicon of a link card (LinkCardDto.ImageUrl and FaviconUrl).
[Route("api/link-cards/{id:guid}")]
public sealed class LinkCardImagesController(LinkCardImages images) : ApiControllerBase
{
    [HttpGet("image")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
        => ResultOf(await images.OpenImageAsync(id, cancellationToken));

    [HttpGet("favicon")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Favicon(Guid id, CancellationToken cancellationToken)
        => ResultOf(await images.OpenFaviconAsync(id, cancellationToken));

    private static LinkImageFileResult ResultOf(BlobContent content) => new(content.Content, content.ContentType);
}

using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Links.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Links;

[Route("api")]
public sealed class LinkCardsController(LinkEndpoints endpoints) : ApiControllerBase
{
    [HttpGet("work-items/{id:guid}/link-cards")]
    [ProducesResponseType<LinkCardsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<LinkCardsDto> List(Guid id, CancellationToken cancellationToken)
        => endpoints.ListAsync(id, cancellationToken);

    [HttpPost("work-items/{id:guid}/link-cards/preview")]
    [ProducesResponseType<LinkPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<LinkPreviewDto> Preview(Guid id, PreviewLinkRequest request, CancellationToken cancellationToken)
        => endpoints.PreviewAsync(id, request, cancellationToken);

    [HttpPost("work-items/{id:guid}/link-cards")]
    [ProducesResponseType<LinkCardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ObjectResult> Add(Guid id, AddLinkCardRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await endpoints.AddUrlAsync(id, request, cancellationToken));

    [HttpPatch("link-cards/{id:guid}")]
    [ProducesResponseType<LinkCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<LinkCardDto> Edit(Guid id, EditLinkCardRequest request, CancellationToken cancellationToken)
        => endpoints.EditAsync(id, request, cancellationToken);

    [HttpPost("link-cards/{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Archive(Guid id, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.ArchiveAsync(id, cancellationToken));
}

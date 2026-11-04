using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

[Route("api/projects/{id:guid}/demo-brand")]
public sealed class DemoBrandController(IDemoBrand brand, IDemoLogoUpload upload, IProjectAccess access) : ApiControllerBase
{
    // The settings preview's <img>. ?v= is the logo_version the brand's LogoUrl carries: the current one is cached for a year, anything else revalidates.
    [HttpGet("logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logo(Guid id, [FromQuery] string? v, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        var logo = await brand.OpenLogoAsync(id, cancellationToken);
        var current = await brand.ReadAsync(id, cancellationToken);
        return new DemoLogoFileResult(logo.Content, logo.ContentType, true, v is not null && current.LogoUrl is { } url && url.EndsWith($"?v={v}", StringComparison.Ordinal));
    }

    // The file arrives as multipart form data. The action binds no form.
    [HttpPut("logo")]
    [StreamedUpload]
    [ProducesResponseType<ProjectDemoBrandDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDemoBrandDto> ReplaceLogo(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await upload.ReplaceAsync(id, Request, cancellationToken);
    }

    [HttpDelete("logo")]
    [ProducesResponseType<ProjectDemoBrandDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDemoBrandDto> RemoveLogo(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await brand.RemoveLogoAsync(id, cancellationToken);
    }

    [HttpPut("tokens")]
    [ProducesResponseType<ProjectDemoBrandDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDemoBrandDto> PutTokens(Guid id, DemoTokensPut put, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await brand.SaveTokensAsync(id, put.Tokens, cancellationToken);
    }
}

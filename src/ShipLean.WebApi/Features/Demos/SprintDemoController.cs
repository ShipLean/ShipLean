using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

[Route("api/sprints/{id:guid}/demo")]
public sealed class SprintDemoController(ISprintDemos demos, IProjectAccess access, IOptions<ForwardedHeadersOptions> forwarding) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SprintDemoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintDemoDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await demos.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    [ProducesResponseType<SprintDemoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<SprintDemoDto> Create(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await demos.CreateFromButtonAsync(id, RequestOrigin.Of(Request, forwarding.Value), cancellationToken);
    }

    [HttpPost("youtube/retry")]
    [ProducesResponseType<SprintDemoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<SprintDemoDto> RetryYouTube(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await demos.RetryYouTubeAsync(id, cancellationToken);
    }
}

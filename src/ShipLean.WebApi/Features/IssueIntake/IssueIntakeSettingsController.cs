using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.IssueIntake.Settings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.IssueIntake;

// A project's GitHub issue intake (C5): members read it, admins change it.
[Route("api/projects/{id:guid}/settings/issue-intake")]
public sealed class IssueIntakeSettingsController(IProjectAccess access, IssueIntakeSettingsService settings) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IssueIntakeSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IssueIntakeSettingsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await settings.GetAsync(id, cancellationToken);
    }

    [HttpPut]
    [ProducesResponseType<IssueIntakeSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IssueIntakeSettingsDto> Put(Guid id, IssueIntakeSaveDto save, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await settings.SaveAsync(id, save, cancellationToken);
    }
}

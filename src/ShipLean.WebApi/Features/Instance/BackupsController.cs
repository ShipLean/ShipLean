using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Instance;

// The instance admin's Backups tab: where backups go, when they run, how many are kept, and the run history.
// GET  /api/instance/backups              -> BackupSettingsDto
// PUT  /api/instance/backups              -> BackupSettingsDto (400 validation with field)
// POST /api/instance/backups/test         -> BackupTestResultDto (a failed probe is ok:false)
// POST /api/instance/backups/cron-preview -> CronPreviewDto (an invalid expression is valid:false)
// POST /api/instance/backups/run          -> 202 BackupRunDto (409 conflict while one runs; 503 not_configured)
[InstanceAdmin]
[Route("api/instance/backups")]
public sealed class BackupsController(IBackupAdmin admin, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BackupSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<BackupSettingsDto> Get(CancellationToken cancellationToken) => admin.GetAsync(cancellationToken);

    [HttpPut]
    [ProducesResponseType<BackupSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<BackupSettingsDto> Put(BackupSaveDto body, CancellationToken cancellationToken) => admin.SaveAsync(body, currentUser.Id, cancellationToken);

    [HttpPost("test")]
    [ProducesResponseType<BackupTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<BackupTestResultDto> Test(BackupDestinationSaveDto body, CancellationToken cancellationToken) => admin.TestAsync(body, cancellationToken);

    [HttpPost("cron-preview")]
    [ProducesResponseType<CronPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<CronPreviewDto> CronPreview(CronPreviewRequestDto body, CancellationToken cancellationToken) => admin.PreviewCronAsync(body.Cron, cancellationToken);

    [HttpPost("run")]
    [ProducesResponseType<BackupRunDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiError>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<AcceptedResult> Run(CancellationToken cancellationToken) => Accepted(await admin.RunNowAsync(currentUser.Id, cancellationToken));
}

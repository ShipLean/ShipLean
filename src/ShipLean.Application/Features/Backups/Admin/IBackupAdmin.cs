using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.Application.Features.Backups.Admin;

// The instance admin's Backups tab, behind the five /api/instance/backups routes. Errors only as ApiException:
// SaveAsync and TestAsync throw validation with Field as the DTO path (destination.s3.bucket, cron, keepCount,
// passphrase, ...); RunNowAsync throws conflict while a backup runs and not_configured when none is set up. A failed
// probe is a result with Ok false, and an expression that doesn't parse is a preview with Valid false, never an error.
public interface IBackupAdmin
{
    Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<BackupSettingsDto> SaveAsync(BackupSaveDto body, Guid userId, CancellationToken cancellationToken);

    Task<BackupTestResultDto> TestAsync(BackupDestinationSaveDto destination, CancellationToken cancellationToken);

    Task<CronPreviewDto> PreviewCronAsync(string cron, CancellationToken cancellationToken);

    Task<BackupRunDto> RunNowAsync(Guid userId, CancellationToken cancellationToken);
}

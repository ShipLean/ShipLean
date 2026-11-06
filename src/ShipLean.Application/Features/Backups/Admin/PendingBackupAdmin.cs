using CronExpressionDescriptor;
using Cronos;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;
using ShipLean.Core;

namespace ShipLean.Application.Features.Backups.Admin;

// TODO: BackupAdmin replaces this placeholder: settings read from backup_settings and the run log, saves, the
// connection test, the cron preview through ICronSchedule and manual runs through IBackupTrigger.
// Until then GET answers the not-configured settings (the default schedule, no destination, no runs) and every other
// call is not_configured (503).
public sealed class PendingBackupAdmin(TimeProvider time, BlobStoreChoice blobStore) : IBackupAdmin
{
    public const string DefaultCron = "0 2 * * *";
    public const int DefaultKeepCount = 14;
    public const string NotReady = "Backups are not available on this instance yet.";

    private const int PreviewRuns = 5;

    public Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new BackupSettingsDto(
        Enabled: false,
        Destination: null,
        Cron: DefaultCron,
        Schedule: DefaultSchedule(),
        KeepCount: DefaultKeepCount,
        IncludeAttachments: false,
        AttachmentStore: new BackupAttachmentStoreDto(StoreKind(blobStore.Kind), InDatabaseBackup: false),
        PassphraseGeneration: 0,
        PassphraseSetAt: null,
        PassphraseSetLabel: null,
        Health: BackupHealth.NotConfigured,
        LastSuccessAt: null,
        LastSuccessLabel: null,
        Runs: [],
        Regions: BackupRegions.All,
        UpdatedAt: null,
        UpdatedLabel: null));

    public Task<BackupSettingsDto> SaveAsync(BackupSaveDto body, Guid userId, CancellationToken cancellationToken)
        => throw ApiException.NotConfigured(NotReady);

    public Task<BackupTestResultDto> TestAsync(BackupDestinationSaveDto destination, CancellationToken cancellationToken)
        => throw ApiException.NotConfigured(NotReady);

    public Task<CronPreviewDto> PreviewCronAsync(string cron, CancellationToken cancellationToken)
        => throw ApiException.NotConfigured(NotReady);

    public Task<BackupRunDto> RunNowAsync(Guid userId, CancellationToken cancellationToken)
        => throw ApiException.NotConfigured(NotReady);

    private CronPreviewDto DefaultSchedule()
    {
        var expression = CronExpression.Parse(DefaultCron);
        var runs = new List<CronRunDto>(PreviewRuns);
        var from = time.GetUtcNow();
        while (runs.Count < PreviewRuns && expression.GetNextOccurrence(from, TimeZoneInfo.Utc) is { } next)
        {
            runs.Add(new CronRunDto(next, DisplayDate.Format(next)));
            from = next;
        }
        var description = ExpressionDescriptor.GetDescription(DefaultCron, new Options { Use24HourTimeFormat = true, Locale = "en" });
        return new CronPreviewDto(Valid: true, Error: null, description, runs);
    }

    private static BackupAttachmentStoreKind StoreKind(BlobStoreKind kind) => kind switch
    {
        BlobStoreKind.Postgres => BackupAttachmentStoreKind.Postgres,
        BlobStoreKind.S3 => BackupAttachmentStoreKind.S3,
        _ => BackupAttachmentStoreKind.LocalDisk,
    };
}

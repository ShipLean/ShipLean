using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Features.Backups.Config;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Identity;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Backups.Admin;

// The Backups tab over the backup_settings row and the run log. Saves are optimistic on updated_at, so two saves that
// read the same row can't both hand out the next passphrase generation. Secrets go in through BackupSecrets and come
// out only as the secret key's last four; the passphrase never comes out.
public sealed class BackupAdmin(
    BackupSettingsRows rows,
    IBackupRunLog runLog,
    ICronSchedule schedule,
    BackupSecrets secrets,
    IBackupDestinationFactory destinations,
    IBackupTrigger trigger,
    BlobStoreChoice blobStore,
    IConfiguration configuration,
    NpgsqlDataSource dataSource,
    TimeProvider time) : IBackupAdmin
{
    public const string DefaultCron = "0 2 * * *";
    public const int DefaultKeepCount = 14;
    public const int HistoryRuns = 20;
    public const string ConflictMessage = "Backup settings changed while you were editing. Reload and try again.";
    public const string NothingSaved = "Set up backups first: save a destination, a schedule and a passphrase.";

    // Some session in this database holds the backup lease (BackupLeaseLock's two-int key shows as objsubid 2).
    private const string LeaseHeldSql =
        """
        SELECT EXISTS (
            SELECT 1 FROM pg_locks l JOIN pg_database d ON d.oid = l.database
            WHERE l.locktype = 'advisory' AND l.classid = @namespace AND l.objid = @key AND l.objsubid = 2 AND l.granted
              AND d.datname = current_database())
        """;

    private const string UsersSql = "SELECT " + UserRefs.Columns + " FROM app_user u WHERE u.id = ANY(@ids)";

    public async Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken)
        => await PresentAsync(await rows.ReadAsync(cancellationToken), cancellationToken);

    public async Task<BackupSettingsDto> SaveAsync(BackupSaveDto body, Guid userId, CancellationToken cancellationToken)
    {
        var s3 = BackupSaveRules.Destination(body.Destination);
        var cron = BackupSaveRules.Cron(body.Cron, schedule);
        var keepCount = BackupSaveRules.KeepCount(body.KeepCount);

        var saved = await rows.ReadAsync(cancellationToken);
        var passphrase = BackupSaveRules.Passphrase(body.Passphrase, saved?.Passphrase is not null);
        var (protectedSecret, secretLast4) = ProtectedSecret(s3, saved);
        var now = time.GetUtcNow();

        var (protectedPassphrase, generation, setAt) = passphrase is null || IsSavedPassphrase(saved, passphrase)
            ? (saved!.Passphrase, saved.PassphraseGeneration, saved.PassphraseSetAt)
            : (secrets.Protect(passphrase), (saved?.PassphraseGeneration ?? 0) + 1, now);

        var row = new BackupSettingsRow(
            body.Enabled,
            BackupStoredDestination.S3Provider,
            Stored(s3).ToJson(),
            protectedSecret,
            secretLast4,
            cron,
            keepCount,
            body.IncludeAttachments,
            protectedPassphrase,
            generation,
            setAt,
            now,
            userId);
        if (!await rows.TryWriteAsync(row, saved?.UpdatedAt, cancellationToken))
        {
            throw ApiException.Conflict(ConflictMessage);
        }
        return await GetAsync(cancellationToken);
    }

    public async Task<BackupTestResultDto> TestAsync(BackupDestinationSaveDto destination, CancellationToken cancellationToken)
    {
        var s3 = BackupSaveRules.Destination(destination);
        var saved = await rows.ReadAsync(cancellationToken);
        var secretKey = PlainSecret(s3, saved);
        var config = new BackupDestinationConfig(
            BackupStoredDestination.S3Provider,
            new S3DestinationConfig(s3.Region, s3.EndpointUrl, s3.Bucket, s3.Prefix, s3.ForcePathStyle, s3.AccessKeyId, secretKey));
        BackupProbeResult probe;
        try
        {
            probe = await destinations.Create(config).ProbeAsync(cancellationToken);
        }
        catch (BackupDestinationException e)
        {
            // The destination could not be made (or refused before writing): nothing was written.
            return new BackupTestResultDto(false, BackupTestStep.Write, e.Message);
        }
        return new BackupTestResultDto(probe.Ok, probe.Ok ? null : Step(probe.Step), probe.Message);
    }

    public Task<CronPreviewDto> PreviewCronAsync(string cron, CancellationToken cancellationToken)
        => Task.FromResult(Preview(cron ?? string.Empty));

    public async Task<BackupRunDto> RunNowAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await rows.ReadAsync(cancellationToken) is null)
        {
            throw ApiException.NotConfigured(NothingSaved);
        }
        var result = await trigger.RequestAsync(BackupTriggerKind.Manual, userId, null, cancellationToken);
        var run = result.Run ?? throw new InvalidOperationException($"A manual backup was refused ({result.Refusal}) without an error.");
        var users = await UsersAsync([run], cancellationToken);
        return BackupRunLabels.ToDto(run, StartedBy(run, users), time);
    }

    private async Task<BackupSettingsDto> PresentAsync(BackupSettingsRow? row, CancellationToken cancellationToken)
    {
        var recent = await runLog.RecentAsync(HistoryRuns, cancellationToken);
        var lastSuccess = await runLog.LastSuccessAsync(cancellationToken);
        var leaseHeld = recent.Count > 0 && recent[0].State == Runs.BackupRunState.Running && await LeaseHeldAsync(cancellationToken);
        var users = await UsersAsync(recent, cancellationToken);
        var health = BackupHealthRule.Compute(row, recent, leaseHeld, lastSuccess, schedule, time.GetUtcNow());
        var cron = row?.Cron ?? DefaultCron;
        var lastSuccessAt = lastSuccess?.FinishedAt;
        return new BackupSettingsDto(
            Enabled: row?.Enabled ?? false,
            Destination: row?.Destination is null ? null : Present(row),
            Cron: cron,
            Schedule: Preview(cron),
            KeepCount: row?.KeepCount ?? DefaultKeepCount,
            IncludeAttachments: row?.IncludeAttachments ?? false,
            AttachmentStore: new BackupAttachmentStoreDto(BackupAttachmentStore.Kind(blobStore.Kind), BackupAttachmentStore.InDatabase(blobStore, configuration)),
            PassphraseGeneration: row?.PassphraseGeneration ?? 0,
            PassphraseSetAt: row?.PassphraseSetAt,
            PassphraseSetLabel: Label(row?.PassphraseSetAt),
            Health: health,
            LastSuccessAt: lastSuccessAt,
            LastSuccessLabel: Label(lastSuccessAt),
            Runs: [.. recent.Select(r => BackupRunLabels.ToDto(r, StartedBy(r, users), time))],
            Regions: BackupRegions.All,
            UpdatedAt: row?.UpdatedAt,
            UpdatedLabel: Label(row?.UpdatedAt));
    }

    private static BackupDestinationDto Present(BackupSettingsRow row)
    {
        var stored = BackupStoredDestination.Parse(row.Destination!);
        return new BackupDestinationDto(
            BackupProvider.S3,
            new S3DestinationDto(stored.Region, stored.EndpointUrl, stored.Bucket, stored.Prefix, stored.ForcePathStyle, stored.AccessKeyId,
                row.DestinationSecret is null ? null : row.SecretLast4));
    }

    private CronPreviewDto Preview(string cron)
    {
        var preview = schedule.Preview(cron, time.GetUtcNow());
        return new CronPreviewDto(preview.Valid, preview.Error, preview.Description, [.. preview.NextRuns.Select(at => new CronRunDto(at, DisplayDate.Format(at)))]);
    }

    private string? Label(DateTimeOffset? value) => value is { } at ? DisplayDate.Relative(at, time) : null;

    // secretKey null keeps the saved secret, "" clears it (the rules already made accessKeyId null), a value replaces it.
    // Keeping needs an access key ID to keep it with and a saved secret to keep.
    private (string? Protected, string? LastFour) ProtectedSecret(S3DestinationSaveDto s3, BackupSettingsRow? saved)
    {
        if (s3.SecretKey is { Length: > 0 } secret)
        {
            return (secrets.Protect(secret), BackupSecrets.LastFour(secret));
        }
        if (s3.SecretKey is not null)
        {
            return (null, null);
        }
        var kept = KeptSecret(s3, saved);
        return kept is null ? (null, null) : (kept, saved!.SecretLast4);
    }

    private string? PlainSecret(S3DestinationSaveDto s3, BackupSettingsRow? saved)
    {
        if (s3.SecretKey is not null)
        {
            return s3.SecretKey.Length == 0 ? null : s3.SecretKey;
        }
        if (KeptSecret(s3, saved) is not { } kept)
        {
            return null;
        }
        return secrets.TryUnprotect(kept)
               ?? throw ApiException.Validation("The saved secret key can no longer be read. Enter the secret key again.", BackupSaveRules.SecretKeyField);
    }

    // The saved secret (still protected) that a null secretKey keeps, or null when there are no keys at all.
    private static string? KeptSecret(S3DestinationSaveDto s3, BackupSettingsRow? saved)
    {
        var savedSecret = saved?.DestinationSecret;
        if (s3.AccessKeyId is null)
        {
            return savedSecret is null
                ? null
                : throw ApiException.Validation("Enter the access key ID, or remove the keys to use the AWS default credential chain.", BackupSaveRules.AccessKeyIdField);
        }
        return savedSecret ?? throw ApiException.Validation("Enter the secret key for this access key ID.", BackupSaveRules.SecretKeyField);
    }

    // A saved passphrase the key ring can no longer open counts as different, so typing it again starts a new generation.
    private bool IsSavedPassphrase(BackupSettingsRow? saved, string passphrase)
        => saved?.Passphrase is { } current && string.Equals(secrets.TryUnprotect(current), passphrase, StringComparison.Ordinal);

    private static BackupStoredDestination Stored(S3DestinationSaveDto s3)
        => new(s3.Region, s3.EndpointUrl, s3.Bucket, s3.Prefix, s3.ForcePathStyle, s3.AccessKeyId);

    private static BackupTestStep Step(string? step) => step switch
    {
        BackupProbeResult.WriteStep => BackupTestStep.Write,
        BackupProbeResult.ReadStep => BackupTestStep.Read,
        BackupProbeResult.DeleteStep => BackupTestStep.Delete,
        _ => throw new InvalidOperationException($"A failed probe named no known step ({step})."),
    };

    private static UserRef? StartedBy(BackupRunRow run, IReadOnlyDictionary<Guid, UserRef> users)
        => run.StartedBy is { } id && users.TryGetValue(id, out var user) ? user : null;

    private async Task<bool> LeaseHeldAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(LeaseHeldSql, BackupLeaseLock.Parameters, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyDictionary<Guid, UserRef>> UsersAsync(IReadOnlyList<BackupRunRow> runs, CancellationToken cancellationToken)
    {
        var ids = runs.Where(r => r.StartedBy is not null).Select(r => r.StartedBy!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, UserRef>();
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = await connection.QueryAsync<UserRef>(new CommandDefinition(UsersSql, new { ids }, cancellationToken: cancellationToken));
        return users.ToDictionary(u => u.Id);
    }
}

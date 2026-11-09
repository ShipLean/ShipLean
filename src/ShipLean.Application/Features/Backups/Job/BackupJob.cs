using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Backups.Job;

// One backup run (contract C-RUN, decisions D3-D6, D8, D9): dump the database through the encryptor into a temporary file,
// upload it, mirror the attachments when the snapshot asks for it, record the run as succeeded, then prune this
// destination's older runs beyond KeepCount. Every step runs under a token linked to lease.Lost. It never reads the config
// (snapshot is the run's) and never throws: a failure is recorded with FailAsync, with a message that holds no secret.
public sealed class BackupJob(
    IBackupRunLog runLog,
    BackupJobEngine engine,
    BackupTempFiles tempFiles,
    BackupJobMetrics metrics,
    IUsageEvents usage,
    TimeProvider time,
    ILogger<BackupJob> logger) : IBackupJob
{
    public const string LeaseLostError = "Stopped: the backup lease was lost, so another instance may be running a backup.";
    public const string ShutdownError = "Stopped: ShipLean shut down during the run.";

    private const string SetupStep = "The destination could not be set up";
    private const string TempFileStep = "The temporary file could not be created";
    private const string EncryptStep = "The encryption could not start";
    private const string DumpStep = "The database dump failed";
    private const string UploadStep = "The upload failed";
    private const string MirrorStep = "Copying the attachments failed";
    private const string RecordStep = "Recording the run failed";

    public async Task RunAsync(BackupLease lease, BackupConfig snapshot, CancellationToken cancellationToken = default)
    {
        var run = lease.Run;
        var succeeded = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lease.Lost);
        var token = linked.Token;
        var step = SetupStep;
        try
        {
            logger.LogInformation("Backup run {RunId} started, writing to {Destination}", run.Id, snapshot.Destination);
            var destination = engine.Destinations.Create(snapshot.Destination);
            var prefix = PrefixOf(snapshot.Destination);
            var key = BackupObjectKeys.Database(prefix, run.StartedAt, run.Id, snapshot.PassphraseGeneration);
            long bytes;
            step = TempFileStep;
            await using (var temp = tempFiles.Create())
            {
                step = EncryptStep;
                await using (var encryptor = engine.Cipher.OpenEncryptor(temp, snapshot.Passphrase))
                {
                    step = DumpStep;
                    await engine.Dumper.DumpAsync(encryptor, token);
                }

                step = UploadStep;
                token.ThrowIfCancellationRequested();
                bytes = temp.Length;
                temp.Position = 0;
                await destination.PutAsync(key, temp, token);
            }

            var mirrored = new MirrorResult(0, 0);
            if (snapshot.IncludeAttachments && !snapshot.AttachmentsInDatabase)
            {
                step = MirrorStep;
                mirrored = await engine.Mirror.MirrorAsync(destination, prefix, snapshot.PassphraseGeneration, snapshot.Passphrase, token);
            }

            step = RecordStep;
            token.ThrowIfCancellationRequested();
            // The new backup counts before anything old is removed.
            succeeded = await runLog.SucceedAsync(run.Id, new BackupOutcome(key, bytes, mirrored.Copied, mirrored.Bytes), token);
            if (!succeeded)
            {
                logger.LogWarning("Backup run {RunId} uploaded {Key} but was no longer running when it finished, so nothing was pruned", run.Id, key);
                return;
            }

            logger.LogInformation("Backup run {RunId} succeeded: {Key}, {Bytes} bytes, {Copied} attachments copied", run.Id, key, bytes, mirrored.Copied);
            await PruneAsync(lease, snapshot, destination, token);
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(run.Id, Describe(step, exception, lease, cancellationToken), exception);
        }
        finally
        {
            Report(run, snapshot, succeeded);
        }
    }

    // A destination config with no settings for its provider can't name a prefix; the factory refuses it the same way.
    private static string PrefixOf(BackupDestinationConfig destination)
        => destination.S3?.Prefix ?? throw new BackupDestinationException($"The {destination.Provider} destination has no settings.");

    // Only messages the contracts promise are secret-free (and IOException's, which name the temporary file) are kept;
    // anything else is named by its type.
    private static string Describe(string step, Exception exception, BackupLease lease, CancellationToken cancellationToken)
    {
        if (lease.Lost.IsCancellationRequested || exception is BackupLeaseLostException)
        {
            return LeaseLostError;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ShutdownError;
        }

        return WithheldException.IsTrusted(exception)
            ? $"{step}: {exception.Message}"
            : $"{step} ({exception.GetType().Name}).";
    }

    // Claims this destination's runs beyond KeepCount on the lease's connection, then deletes only the claimed objects. A
    // failure here leaves the run succeeded: a failed claim prunes nothing, and a claimed row whose delete failed stays
    // claimed, so the warning names its key for manual cleanup.
    private async Task PruneAsync(BackupLease lease, BackupConfig snapshot, IBackupDestination destination, CancellationToken token)
    {
        IReadOnlyList<BackupRunRow> claimed = [];
        Exception? claimFailure = null;
        try
        {
            claimed = await runLog.ClaimPruneAsync(lease, snapshot.DestinationId, snapshot.KeepCount, token);
        }
        catch (Exception exception)
        {
            claimFailure = exception;
        }

        if (claimFailure is not null)
        {
            logger.LogWarning(WithheldException.Loggable(claimFailure), "Backup run {RunId} succeeded, but no older backups were pruned: claiming them failed", lease.Run.Id);
            return;
        }

        foreach (var old in claimed)
        {
            if (old.DatabaseObjectKey is not { } key)
            {
                logger.LogWarning("Pruned backup run {RunId} has no database object to delete", old.Id);
                continue;
            }

            if (await TryDeleteAsync(destination, key, token) is { } deleteFailure)
            {
                logger.LogWarning(WithheldException.Loggable(deleteFailure), "Could not delete old backup {Key} of run {RunId}; its run is marked pruned, so delete it by hand", key, old.Id);
                continue;
            }

            logger.LogInformation("Pruned backup {Key} of run {RunId}", key, old.Id);
        }
    }

    // The delete's failure, or null when it worked.
    private static async Task<Exception?> TryDeleteAsync(IBackupDestination destination, string key, CancellationToken token)
    {
        try
        {
            await destination.DeleteAsync(key, token);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task RecordFailureAsync(Guid runId, string error, Exception exception)
    {
        logger.LogError(WithheldException.Loggable(exception), "Backup run {RunId} failed: {Error}", runId, error);
        bool? recorded = null;
        Exception? recordFailure = null;
        try
        {
            recorded = await runLog.FailAsync(runId, error, CancellationToken.None);
        }
        catch (Exception recording)
        {
            recordFailure = recording;
        }

        if (recordFailure is not null)
        {
            logger.LogError(WithheldException.Loggable(recordFailure), "Could not record the failure of backup run {RunId}", runId);
        }
        else if (recorded == false)
        {
            logger.LogWarning("Backup run {RunId} was no longer running when its failure was recorded", runId);
        }
    }

    private void Report(BackupRunRow run, BackupConfig snapshot, bool succeeded)
    {
        metrics.RunFinished(succeeded ? BackupJobMetrics.Succeeded : BackupJobMetrics.Failed);
        var now = time.GetUtcNow();
        usage.Track(new UsageEvent(
            UsageEventNames.BackupRunFinished, UsageSurface.System, UsageActor.System, null, 0, succeeded ? UsageOutcome.Ok : UsageOutcome.Error, null,
            (long)(now - run.StartedAt).TotalMilliseconds, $"system:{nameof(BackupJob)}", now,
            new Dictionary<string, object> { [UsageProps.Provider] = snapshot.Destination.Provider, [UsageProps.Trigger] = BackupRunWire.Trigger(run.Trigger) }));
    }
}

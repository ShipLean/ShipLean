using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Usage;
using ShipLean.Core.Telemetry;

namespace ShipLean.Application.Features.Import.Jobs;

// The hosted queue that runs imports, one at a time, inside the one process (no separate worker). A job that was queued, or running when the process stopped, is picked up
// again on start. It starts without the starter's execution context, so nothing it does carries the request or user of whoever started the host.
public sealed class ImportRunner(IServiceScopeFactory scopes, ImportQueue queue, ImportJobRows rows, IUsageEvents usage, TimeProvider time, ILogger<ImportRunner> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Task started;
        using (ExecutionContext.SuppressFlow())
        {
            started = base.StartAsync(cancellationToken);
        }

        return started;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ResumeAsync(stoppingToken);
            await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
            {
                await RunOneAsync(jobId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task ResumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var jobId in await rows.ResumeInterruptedAsync(stoppingToken))
            {
                queue.Enqueue(jobId);
            }
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not look for import jobs to resume");
        }
    }

    // An exception out of a BackgroundService would stop the host, so a job that blows up is logged and the next one runs.
    private async Task RunOneAsync(Guid jobId, CancellationToken stoppingToken)
    {
        using var activity = ShipLeanActivities.Source.StartActivity("import.job");
        activity?.SetTag("shiplean.import.job_id", jobId);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ImportJobRun>().ExecuteAsync(jobId, stoppingToken);
            await TrackFinishedAsync(jobId, stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            logger.LogError(failed, "Import job {JobId} could not run", jobId);
            await FailRunningAsync(jobId);
            await TrackFinishedAsync(jobId, CancellationToken.None);
        }
    }

    // After the job's last write: Done and DoneWithErrors are completed, Failed is failed; a stopped (or untouched) job is neither.
    private async Task TrackFinishedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            if (await rows.FindAsync(jobId, cancellationToken) is not { } job)
            {
                return;
            }

            var (name, outcome) = ImportJobRows.StatusOf(job.Status) switch
            {
                ImportJobState.Done or ImportJobState.DoneWithErrors => (UsageEventNames.ImportCompleted, UsageOutcome.Ok),
                ImportJobState.Failed => (UsageEventNames.ImportFailed, UsageOutcome.Error),
                _ => (null, UsageOutcome.Ok),
            };
            if (name is null)
            {
                return;
            }

            long? duration = job.StartedAt is { } started && job.FinishedAt is { } finished ? (long)(finished - started).TotalMilliseconds : null;
            usage.Track(new UsageEvent(
                name, UsageSurface.System, UsageActor.System, job.ProjectId, job.ProjectId is null ? 0 : 1, outcome, null, duration,
                $"system:{nameof(ImportRunner)}", time.GetUtcNow(), new Dictionary<string, object> { [UsageProps.Provider] = "jira" }));
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not track the end of import job {JobId}", jobId);
        }
    }

    // The job's own failure handling threw (a database blip), which would leave it Running for ever: stop and re-run both refuse a running job.
    private async Task FailRunningAsync(Guid jobId)
    {
        try
        {
            await rows.FailRunningAsync(jobId, "The import stopped on an unexpected error. The server log has the details.", CancellationToken.None);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Import job {JobId} could not be marked as failed", jobId);
        }
    }
}

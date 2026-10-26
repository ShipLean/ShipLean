using System.Text.RegularExpressions;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Import.Jobs;

// IImportJobs over the import tables. Start and Re-run queue the job for the runner (ImportRunner); retry works the failed rows in the caller's request, because it
// is one row, or the few that failed, not a whole project. Callers check project access first.
public sealed partial class ImportJobs(ImportJobRows rows, ImportQueue queue, ImportNotifier notifier, ImportRetry retry, ICurrentUser currentUser) : IImportJobs
{
    public async Task<ImportJobDto> StartAsync(ImportStartRequest request, CancellationToken cancellationToken = default)
    {
        var jiraKey = (request.JiraProjectKey ?? string.Empty).Trim();
        if (jiraKey.Length == 0)
        {
            throw ApiException.Validation("Choose the Jira project to import.");
        }

        var target = request.Target;
        if (target.Kind == ImportTargetKind.NewProject && (target.Key is null || !ProjectKey().IsMatch(target.Key)))
        {
            throw ApiException.Validation("A project key is 2 to 4 capital letters.");
        }
        if (target.Kind == ImportTargetKind.NewProject && string.IsNullOrWhiteSpace(target.Name))
        {
            throw ApiException.Validation("Give the new project a name.");
        }
        if (target.Kind == ImportTargetKind.ExistingProject && target.ProjectId is null)
        {
            throw ApiException.Validation("Choose the empty project to import into.");
        }

        if (!await rows.ConnectionExistsAsync(request.ConnectionId, cancellationToken))
        {
            throw ApiException.NotFound("That Jira connection does not exist.");
        }

        string projectKey;
        if (target.Kind == ImportTargetKind.ExistingProject)
        {
            projectKey = await rows.ProjectKeyOfAsync(target.ProjectId!.Value, cancellationToken) ?? throw ApiException.NotFound("That project does not exist.");
            if (await rows.ProjectHasWorkItemsAsync(target.ProjectId.Value, cancellationToken))
            {
                throw ApiException.Conflict("That project already has work items; choose an empty one.");
            }
        }
        else
        {
            projectKey = target.Key!;
            if (await rows.ProjectKeyTakenAsync(projectKey, cancellationToken))
            {
                throw ApiException.Conflict($"The key {projectKey} is already taken.");
            }
        }

        var projectId = target.Kind == ImportTargetKind.ExistingProject ? target.ProjectId : null;
        if (await rows.ActiveJobExistsAsync(request.ConnectionId, jiraKey, projectId, projectKey, cancellationToken))
        {
            throw ApiException.Conflict($"{jiraKey} is already being imported, or another import is already creating {projectKey}.");
        }

        var id = Guid.CreateVersion7();
        var normalised = request with { JiraProjectKey = jiraKey, Target = target with { Name = target.Name?.Trim() } };
        await rows.InsertAsync(id, normalised, projectKey, projectId, currentUser.Id, $"Queued the import of {jiraKey}", cancellationToken);
        var started = await ChangedAsync(id, cancellationToken);
        queue.Enqueue(id);
        return started;
    }

    public async Task<ImportJobDto> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
        => rows.ToDto(await rows.GetAsync(jobId, cancellationToken));

    public async Task<IReadOnlyList<ImportJobDto>> ListAsync(Guid? projectId, CancellationToken cancellationToken = default)
        => [.. (await rows.ListAsync(projectId, currentUser.Id, cancellationToken)).Select(rows.ToDto)];

    public async Task<ImportJobDto> StopAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await rows.GetAsync(jobId, cancellationToken);
        var state = ImportJobRows.StatusOf(job.Status);
        if (state is not (ImportJobState.Queued or ImportJobState.Running))
        {
            throw ApiException.Conflict("That import already finished.");
        }

        var progress = ImportJson.Read<ImportJobProgressJson>(job.Progress);
        if (state == ImportJobState.Queued && await rows.StopQueuedAsync(jobId, progress with { Message = "Stopped before it started" }, cancellationToken))
        {
            await rows.LogAsync(jobId, ImportLogLevel.Info, "Stopped before it started", cancellationToken);
            return await ChangedAsync(jobId, cancellationToken);
        }

        // Running (or started just now): the runner looks at the request between batches.
        queue.RequestStop(jobId);
        await rows.LogAsync(jobId, ImportLogLevel.Info, "Stop requested: finishing the current batch", cancellationToken);
        return await GetAsync(jobId, cancellationToken);
    }

    public async Task<ImportJobDto> RetryAsync(Guid errorId, CancellationToken cancellationToken = default)
    {
        var error = await FindPendingErrorAsync(errorId, cancellationToken);
        var job = await rows.GetAsync(error.JobId, cancellationToken);
        RequireSettled(job);
        await retry.RunAsync(job, [error], cancellationToken);
        return await ChangedAsync(job.Id, cancellationToken);
    }

    public async Task<ImportJobDto> RetryAllAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await rows.GetAsync(jobId, cancellationToken);
        var pending = await rows.PendingErrorsAsync(jobId, cancellationToken);
        if (pending.Count == 0)
        {
            throw ApiException.Conflict("Nothing is waiting to be retried.");
        }

        RequireSettled(job);
        await retry.RunAsync(job, pending, cancellationToken);
        return await ChangedAsync(jobId, cancellationToken);
    }

    public async Task<ImportJobDto> SkipAsync(Guid errorId, CancellationToken cancellationToken = default)
    {
        var error = await FindPendingErrorAsync(errorId, cancellationToken);
        if (!await rows.ResolveErrorAsync(errorId, ImportErrorResolution.Skipped, cancellationToken))
        {
            throw ApiException.Conflict("That error is already resolved.");
        }

        await rows.LogAsync(error.JobId, ImportLogLevel.Info, $"Skipped {error.SourceKey}", cancellationToken);
        await rows.FinishIfResolvedAsync(error.JobId, cancellationToken);
        return await ChangedAsync(error.JobId, cancellationToken);
    }

    public async Task<ImportJobDto> RerunAsync(Guid jobId, ImportMappingDto? mapping, CancellationToken cancellationToken = default)
    {
        var job = await rows.GetAsync(jobId, cancellationToken);
        if (ImportJobRows.StatusOf(job.Status) is ImportJobState.Queued or ImportJobState.Running)
        {
            throw ApiException.Conflict("That import is still going.");
        }

        if (!await rows.RequeueAsync(jobId, currentUser.Id, ProgressDocs.Initial(rerun: true), cancellationToken))
        {
            throw ApiException.Conflict("That import is still going.");
        }

        if (mapping is not null)
        {
            await rows.SaveMappingAsync(jobId, mapping, cancellationToken);
        }

        await rows.LogAsync(jobId, ImportLogLevel.Info, $"Queued a re-run of {job.JiraProjectKey}", cancellationToken);
        var rerun = await ChangedAsync(jobId, cancellationToken);
        queue.Enqueue(jobId);
        return rerun;
    }

    public async Task<ImportMappingDto> GetMappingAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await rows.GetAsync(jobId, cancellationToken);
        return await rows.ReadMappingAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<ImportLogLineDto>> GetLogAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await rows.GetAsync(jobId, cancellationToken);
        return await rows.LogLinesAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<ImportErrorDto>> GetErrorsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await rows.GetAsync(jobId, cancellationToken);
        return [.. (await rows.ErrorsAsync(jobId, cancellationToken)).Select(ImportJobRows.ToDto)];
    }

    private async Task<ImportErrorRow> FindPendingErrorAsync(Guid errorId, CancellationToken cancellationToken)
    {
        var error = await rows.FindErrorAsync(errorId, cancellationToken) ?? throw ApiException.NotFound("That error does not exist.");
        return error.Resolution == ImportErrorRow.Pending ? error : throw ApiException.Conflict("That error is already resolved.");
    }

    private static void RequireSettled(ImportJobRow job)
    {
        if (ImportJobRows.StatusOf(job.Status) is ImportJobState.Queued or ImportJobState.Running)
        {
            throw ApiException.Conflict("Wait for the import to finish before retrying its errors.");
        }
    }

    private async Task<ImportJobDto> ChangedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await rows.GetAsync(jobId, cancellationToken);
        await notifier.NotifyAsync(job);
        return rows.ToDto(job);
    }

    [GeneratedRegex("^[A-Z]{2,4}$")]
    private static partial Regex ProjectKey();
}

namespace ShipLean.Contracts.Import;

// Jira import jobs: start, watch, stop, retry, skip, re-run. Callers check project access first. Every method throws ApiException.NotFound for an unknown job or error id.
public interface IImportJobs
{
    // Queues the job. Throws ApiException.NotFound (unknown connection or existing target project), Validation (bad target key or name, blank Jira project key), Conflict (key taken, target project not empty, a job for this Jira project is already queued or running).
    Task<ImportJobDto> StartAsync(ImportStartRequest request, CancellationToken cancellationToken = default);

    Task<ImportJobDto> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Newest first; projectId null = every job the caller can see.
    Task<IReadOnlyList<ImportJobDto>> ListAsync(Guid? projectId, CancellationToken cancellationToken = default);

    // Asks the job to stop after the current batch (a queued job stops at once); the returned job is Running or Stopped. Throws ApiException.Conflict for a job that already finished.
    Task<ImportJobDto> StopAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Tries the failed item again and marks the error Retried; the job moves from DoneWithErrors to Done once no error is pending. Throws ApiException.Conflict for an error already resolved.
    Task<ImportJobDto> RetryAsync(Guid errorId, CancellationToken cancellationToken = default);

    // Retries every pending error of the job. Throws ApiException.Conflict when the job has none.
    Task<ImportJobDto> RetryAllAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Marks the error Skipped without trying again; the same DoneWithErrors-to-Done rule. Throws ApiException.Conflict for an error already resolved.
    Task<ImportJobDto> SkipAsync(Guid errorId, CancellationToken cancellationToken = default);

    // The same row back to Queued with Progress.Rerun true, using the edited mapping when one is given and the saved one when null. Errors still pending are carried over as pending, so the job ends DoneWithErrors again
    // unless they are retried or skipped. Throws ApiException.Conflict for a job that is queued or running.
    Task<ImportJobDto> RerunAsync(Guid jobId, ImportMappingDto? mapping, CancellationToken cancellationToken = default);

    // The mapping the job runs with: the one it started with, or the last one a re-run was given. Re-runs and the mapping step read it to keep earlier choices.
    Task<ImportMappingDto> GetMappingAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Oldest first.
    Task<IReadOnlyList<ImportLogLineDto>> GetLogAsync(Guid jobId, CancellationToken cancellationToken = default);

    // Pending errors first, then resolved ones, each oldest first.
    Task<IReadOnlyList<ImportErrorDto>> GetErrorsAsync(Guid jobId, CancellationToken cancellationToken = default);
}

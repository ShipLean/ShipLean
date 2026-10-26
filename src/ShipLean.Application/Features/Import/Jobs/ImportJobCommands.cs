using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jobs;

// The import actions that need more than "may this admin touch this job": each checks who may do it, validates what the request names, then hands over to IImportJobs or
// IImportMappings, so the controller's actions stay one call. Imports belong to project admins (ImportJobAccess).
[RegisterPerRequest]
public sealed class ImportJobCommands(IImportJobs jobs, IImportMappings mappings, ImportJobAccess access, IProjectAccess projects)
{
    public async Task<ImportJobDto> StartAsync(ImportStartRequest request, CancellationToken cancellationToken)
    {
        await access.RequireConnectionAsync(request.ConnectionId, cancellationToken);
        if (request.Target is { Kind: ImportTargetKind.ExistingProject, ProjectId: { } target })
        {
            await projects.RequireAsync(target, ProjectRole.Admin, cancellationToken);
        }

        return await jobs.StartAsync(request, cancellationToken);
    }

    // Retries one error, or every pending error of the job when no error id is given.
    public async Task<ImportJobDto> RetryAsync(Guid jobId, Guid? errorId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        if (errorId is not { } error)
        {
            return await jobs.RetryAllAsync(jobId, cancellationToken);
        }

        await access.RequireErrorAsync(jobId, error, cancellationToken);
        return await jobs.RetryAsync(error, cancellationToken);
    }

    public async Task<ImportJobDto> SkipAsync(Guid jobId, Guid? errorId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        if (errorId is not { } error)
        {
            throw ApiException.Validation("Choose the error to skip.");
        }

        await access.RequireErrorAsync(jobId, error, cancellationToken);
        return await jobs.SkipAsync(error, cancellationToken);
    }

    public async Task<ImportMappingDto> SuggestMappingAsync(Guid? connectionId, string? jiraProjectKey, Guid? previousJobId, CancellationToken cancellationToken)
    {
        if (connectionId is not { } connection)
        {
            throw ApiException.Validation("Choose the Jira connection.");
        }

        await access.RequireConnectionAsync(connection, cancellationToken);
        if (previousJobId is { } previous)
        {
            await access.RequireJobAsync(previous, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(jiraProjectKey))
        {
            throw ApiException.Validation("Choose the Jira project to import.");
        }

        return await mappings.SuggestAsync(connection, jiraProjectKey.Trim(), previousJobId, cancellationToken);
    }
}

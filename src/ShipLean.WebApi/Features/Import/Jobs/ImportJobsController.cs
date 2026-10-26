using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Import.Jobs;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Import.Jobs;

// The import's jobs: start one, watch it, stop it, retry or skip its failed rows, run it again. Project admins only: of the project that owns the Jira connection or of the project
// the import went into.
[Route("api/import")]
public sealed class ImportJobsController(IImportJobs jobs, ImportJobCommands commands, ImportJobAccess access, IProjectAccess projects) : ApiControllerBase
{
    [HttpPost("jobs")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<ImportJobDto> Start(ImportStartRequest request, CancellationToken cancellationToken) => commands.StartAsync(request, cancellationToken);

    // Every job of a project the caller administers, newest first.
    [HttpGet("jobs")]
    [ProducesResponseType<IReadOnlyList<ImportJobDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ImportJobDto>> List(CancellationToken cancellationToken) => jobs.ListAsync(null, cancellationToken);

    // The jobs that went into one project.
    [HttpGet("~/api/projects/{id:guid}/import/jobs")]
    [ProducesResponseType<IReadOnlyList<ImportJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<ImportJobDto>> ListForProject(Guid id, CancellationToken cancellationToken)
    {
        await projects.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await jobs.ListAsync(id, cancellationToken);
    }

    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ImportJobDto> Get(Guid jobId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        return await jobs.GetAsync(jobId, cancellationToken);
    }

    [HttpPost("jobs/{jobId:guid}/stop")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ImportJobDto> Stop(Guid jobId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        return await jobs.StopAsync(jobId, cancellationToken);
    }

    // Retries one error, or every pending error of the job when no error id is given.
    [HttpPost("jobs/{jobId:guid}/retry")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<ImportJobDto> Retry(Guid jobId, ImportRetryRequest request, CancellationToken cancellationToken) => commands.RetryAsync(jobId, request.ErrorId, cancellationToken);

    [HttpPost("jobs/{jobId:guid}/skip")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<ImportJobDto> Skip(Guid jobId, ImportSkipRequest request, CancellationToken cancellationToken) => commands.SkipAsync(jobId, request.ErrorId, cancellationToken);

    [HttpPost("jobs/{jobId:guid}/rerun")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<ImportJobDto> Rerun(Guid jobId, ImportRerunRequest request, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        return await jobs.RerunAsync(jobId, request.Mapping, cancellationToken);
    }

    [HttpGet("jobs/{jobId:guid}/log")]
    [ProducesResponseType<IReadOnlyList<ImportLogLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<ImportLogLineDto>> Log(Guid jobId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        return await jobs.GetLogAsync(jobId, cancellationToken);
    }

    [HttpGet("jobs/{jobId:guid}/errors")]
    [ProducesResponseType<IReadOnlyList<ImportErrorDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<ImportErrorDto>> Errors(Guid jobId, CancellationToken cancellationToken)
    {
        await access.RequireJobAsync(jobId, cancellationToken);
        return await jobs.GetErrorsAsync(jobId, cancellationToken);
    }
}

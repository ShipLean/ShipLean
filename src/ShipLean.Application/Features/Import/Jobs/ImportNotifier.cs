using Agoda.IoC.Core;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Import.Jobs;

// Publishes importProgress for a job. A job whose new project is not created yet has no project to publish to, so it says nothing.
[RegisterSingleton]
public sealed class ImportNotifier(IProjectEvents events)
{
    public Task NotifyAsync(ImportJobRow job)
        => NotifyAsync(job.ProjectId, job.Id, ImportJobRows.StatusOf(job.Status), ImportJson.Read<ImportJobProgressJson>(job.Progress));

    public async Task NotifyAsync(Guid? projectId, Guid jobId, ImportJobState state, ImportJobProgressJson progress)
    {
        if (projectId is { } project)
        {
            await events.PublishAsync(new ImportProgress(project, jobId, progress.Percent, state, progress.Message));
        }
    }
}

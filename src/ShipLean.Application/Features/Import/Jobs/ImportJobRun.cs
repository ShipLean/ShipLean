using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Import.Apply;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.Import.Jobs;

// One pass of one queued job, in a DI scope of its own (the stores are per scope and a run is not safe to share). It acts as the person who started the import, so the
// work items, the new project's admin and the revisions name them; it runs outside any request, so its events carry no client id. Jira being down, the credentials being
// refused or the project key being taken ends the job as Failed with that message; a row that cannot be written becomes an import_error and the run goes on.
[RegisterPerRequest]
public sealed class ImportJobRun(
    ImportJobRows rows, ImportQueue queue, ImportNotifier notifier, IJiraConnections connections, IJiraClient jira, IImportApplier applier,
    IProjectFactory projects, IServiceProvider services, ILogger<ImportJobRun> logger)
{
    private static readonly IReadOnlyList<int> WorkingDays = [1, 2, 3, 4, 5];

    private sealed class Tally
    {
        public int Total { get; set; }

        public int Done { get; set; }

        public int Added { get; set; }

        public int Updated { get; set; }

        public int Comments { get; set; }

        public int Attachments { get; set; }

        public int Revisions { get; set; }

        public int Sprints { get; set; }
    }

    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await rows.FindAsync(jobId, cancellationToken);
        if (job is null || ImportJobRows.StatusOf(job.Status) != ImportJobState.Queued)
        {
            return;
        }

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = ActorContext(job.CreatorId ?? JiraImportUser.Id);
        try
        {
            await RunAsync(job, cancellationToken);
        }
        finally
        {
            accessor.HttpContext = null;
            queue.ClearStop(jobId);
        }
    }

    private async Task RunAsync(ImportJobRow job, CancellationToken cancellationToken)
    {
        var previous = ImportJson.Read<ImportJobProgressJson>(job.Progress);
        var progress = previous with { Percent = 0, Step = ImportStep.Project, Message = "Starting" };
        try
        {
            queue.ClearStop(job.Id);
            if (!await rows.MarkRunningAsync(job.Id, progress, cancellationToken))
            {
                return;
            }

            await rows.LogAsync(job.Id, ImportLogLevel.Info, previous.Rerun ? $"Started a re-run of {job.JiraProjectKey}" : $"Started the import of {job.JiraProjectKey}", cancellationToken);
            await Notify(job.ProjectId, job.Id, ImportJobState.Running, progress);
            await RequireActorAsync(job, cancellationToken);
            var connectionId = await rows.CurrentConnectionAsync(job.Id, cancellationToken)
                ?? throw ApiException.NotFound("This project has no Jira connection any more. Save one under Integrations, then run the import again.");
            var credentials = await connections.GetCredentialsAsync(connectionId, cancellationToken);
            var mapping = await rows.ReadMappingAsync(job.Id, cancellationToken);
            await ImportAsync(job, credentials, mapping, progress, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The process is stopping: the job stays running and is queued again on the next start.
            throw;
        }
        catch (ApiException refused)
        {
            await FailAsync(job, progress, refused.Message);
        }
        catch (Exception unexpected)
        {
            logger.LogError(unexpected, "Import job {JobId} failed", job.Id);
            await FailAsync(job, progress, "The import stopped on an unexpected error. The server log has the details.");
        }
    }

    // The run acts as the person who last started it. If they have lost the right to since (switched off, or no longer an admin), the job fails rather than create a project
    // or items in their name.
    private async Task RequireActorAsync(ImportJobRow job, CancellationToken cancellationToken)
    {
        var who = job.CreatorName ?? "The person who started this import";
        var problem = job.CreatorId is { } actor
            ? await rows.ActorProblemAsync(job.Id, actor, cancellationToken)
            : "nobody is recorded as having started it";
        if (problem is not null)
        {
            throw ApiException.Forbidden($"{who} can no longer run this import: {problem}. An admin of the project can run it again with Re-run.");
        }
    }

    private async Task<Guid> CreateProjectAsync(ImportJobRow job, CancellationToken cancellationToken)
    {
        var target = await rows.ReadTargetAsync(job.Id, cancellationToken) ?? throw ApiException.Validation("The import has no target project.");
        if (await rows.ProjectKeyTakenAsync(job.ProjectKey, cancellationToken))
        {
            // A run that died between creating the project and recording it left the project behind: take it back if it is plainly this job's own.
            var adopted = (job.CreatorId is { } actor ? await rows.FindAdoptableProjectAsync(job.ProjectKey, actor, cancellationToken) : null)
                ?? throw ApiException.Conflict($"The key {job.ProjectKey} is already taken, and the project that has it is not an empty one that only {job.CreatorName ?? "the person who started this import"} administers.");
            await rows.SetProjectAsync(job.Id, adopted, cancellationToken);
            await rows.LogAsync(job.Id, ImportLogLevel.Info, $"Carried on into the project {job.ProjectKey} that an earlier run created", cancellationToken);
            return adopted;
        }

        // No first sprint: Jira's own sprints are created by name, and a default "Sprint 1" would be adopted by a Jira sprint of that name.
        var created = await projects.CreateAsync(new ProjectCreate(target.Name ?? job.ProjectKey, job.ProjectKey, null, [], WorkingDays, null), cancellationToken);
        await rows.SetProjectAsync(job.Id, created.Id, cancellationToken);
        await rows.LogAsync(job.Id, ImportLogLevel.Info, $"Created the project {job.ProjectKey}", cancellationToken);
        return created.Id;
    }

    private async Task ImportAsync(ImportJobRow job, JiraCredentials credentials, ImportMappingDto mapping, ImportJobProgressJson progress, CancellationToken cancellationToken)
    {
        var rerun = progress.Rerun;
        progress = progress with
        {
            Step = ImportStep.Issues,
            UsersMatched = mapping.Users.Count(u => u.Resolution != UserResolution.Create),
            UsersCreated = mapping.Users.Count(u => u.Resolution == UserResolution.Create),
            StatesCreated = mapping.Statuses.Count(s => s.CreatesState),
        };

        // Pass 1 reads the whole Jira project but keeps only its sprints and a count, so memory stays one page. Pass 2 reads it again and applies one page at a time.
        var scan = await ScanAsync(job, credentials, mapping, progress, cancellationToken);
        if (scan is null)
        {
            await StopAsync(job, job.ProjectId, progress, new Tally(), "Stopped before any issue was imported", cancellationToken);
            return;
        }

        // The project is created only once Jira has answered, so a Jira that is down or refuses the token leaves no empty project behind.
        var projectId = job.ProjectId ?? await CreateProjectAsync(job, cancellationToken);
        await Notify(projectId, job.Id, ImportJobState.Running, progress);
        var tally = new Tally { Total = scan.Total, Sprints = scan.Sprints.Count };

        var run = applier.Begin(projectId, job.Id, credentials, mapping);
        await applier.PrepareAsync(run, scan.Sprints, cancellationToken);
        foreach (var warning in run.Warnings)
        {
            await rows.LogAsync(job.Id, ImportLogLevel.Warning, warning, cancellationToken);
        }

        var deferred = new List<JiraIssue>();
        var (numberFields, continuation) = (NumberFields(mapping), (string?)null);
        int pagesApplied = 0, issuesApplied = 0;
        do
        {
            if (queue.IsStopRequested(job.Id))
            {
                await StopAsync(job, projectId, progress, tally, $"Stopped after {tally.Done} of {tally.Total} issues", cancellationToken);
                return;
            }

            var page = await jira.SearchIssuesAsync(credentials, job.JiraProjectKey, continuation, ImportRetry.PageSize, numberFields, cancellationToken);
            issuesApplied += page.Issues.Count;
            ImportRetry.RequireWithinLimits(++pagesApplied, issuesApplied);
            foreach (var issue in page.Issues)
            {
                await ApplyAsync(run, mapping, issue, tally, deferred, cancellationToken);
            }

            continuation = page.Continuation;
            progress = await ReportAsync(job, projectId, progress, tally, cancellationToken);
        }
        while (continuation is not null);

        foreach (var issue in deferred)
        {
            await ReapplyAsync(job, run, issue, tally, cancellationToken);
        }

        await FinishAsync(job, projectId, progress, tally, rerun, cancellationToken);
    }

    private sealed record Scan(int Total, IReadOnlyList<JiraSprint> Sprints);

    private static string[] NumberFields(ImportMappingDto mapping) => mapping.Fields.StoryPointsFieldId.Length > 0 ? [mapping.Fields.StoryPointsFieldId] : [];

    // Pass 1. Null when a stop was asked for while reading, before any project or item exists.
    private async Task<Scan?> ScanAsync(ImportJobRow job, JiraCredentials credentials, ImportMappingDto mapping, ImportJobProgressJson progress, CancellationToken cancellationToken)
    {
        var sprints = new Dictionary<long, JiraSprint>();
        string? continuation = null;
        var read = 0;
        var total = 0;
        var pages = 0;
        do
        {
            if (queue.IsStopRequested(job.Id))
            {
                return null;
            }

            var page = await jira.SearchIssuesAsync(credentials, job.JiraProjectKey, continuation, ImportRetry.PageSize, NumberFields(mapping), cancellationToken);
            read += page.Issues.Count;
            ImportRetry.RequireWithinLimits(++pages, read);
            total += page.Issues.Count(i => IsImported(mapping, i));
            foreach (var sprint in page.Issues.SelectMany(i => i.Sprints))
            {
                sprints.TryAdd(sprint.Id, sprint);
            }

            continuation = page.Continuation;
            await SaveAndNotifyAsync(job.Id, job.ProjectId, ImportJobState.Running, progress with { Message = $"Reading issues from Jira ({read} so far)" }, cancellationToken);
        }
        while (continuation is not null);

        await rows.LogAsync(job.Id, ImportLogLevel.Info, $"Read {read} issues from Jira to find the sprints and count the work; Jira is read a second time to import them, one page at a time", cancellationToken);
        return new Scan(total, [.. sprints.Values]);
    }

    private async Task ApplyAsync(ImportRun run, ImportMappingDto mapping, JiraIssue issue, Tally tally, List<JiraIssue> deferred, CancellationToken cancellationToken)
    {
        var outcome = await applier.ApplyIssueAsync(run, issue, cancellationToken);
        if (outcome.Failed || outcome.ItemId is not { } itemId)
        {
            return;
        }

        tally.Done++;
        if (outcome.Created)
        {
            tally.Added++;
        }
        else
        {
            tally.Updated++;
        }

        if (outcome.UnresolvedKeys.Count > 0)
        {
            deferred.Add(issue);
        }

        tally.Comments += await applier.ApplyCommentsAsync(run, issue, itemId, cancellationToken);
        tally.Attachments += await applier.ApplyAttachmentsAsync(run, issue, itemId, cancellationToken);
        tally.Revisions += mapping.Fields.IncludeChangelog ? issue.Changelog.Count : 0;
        if (issue.Changelog.Count >= JiraChangelogLimit)
        {
            await rows.LogAsync(run.JobId, ImportLogLevel.Warning, $"{issue.Key} has {issue.Changelog.Count} changelog entries, which is Jira's limit in a search: its history may be cut short", cancellationToken);
        }
    }

    // Jira serves at most 100 changelog entries inline with a search.
    private const int JiraChangelogLimit = 100;

    // The issue's parent or links were applied after it; applying it again (an update, never a second item) lets them resolve.
    private async Task ReapplyAsync(ImportJobRow job, ImportRun run, JiraIssue issue, Tally tally, CancellationToken cancellationToken)
    {
        var outcome = await applier.ApplyIssueAsync(run, issue, cancellationToken);
        if (outcome.Failed)
        {
            tally.Done--;
        }
        else if (outcome.UnresolvedKeys.Count > 0)
        {
            await rows.LogAsync(job.Id, ImportLogLevel.Warning, $"{issue.Key}: {string.Join(", ", outcome.UnresolvedKeys)} was not imported, so the parent or link was left out", cancellationToken);
        }
    }

    private async Task<ImportJobProgressJson> ReportAsync(ImportJobRow job, Guid projectId, ImportJobProgressJson progress, Tally tally, CancellationToken cancellationToken)
    {
        var percent = tally.Total == 0 ? 0 : Math.Min(99, tally.Done * 100 / tally.Total);
        var next = progress with
        {
            Percent = percent,
            Step = ImportStep.Issues,
            Message = $"Imported {tally.Done} of {tally.Total} issues",
            Counts = Counts(tally, new Dictionary<string, int>()),
        };
        await SaveAndNotifyAsync(job.Id, projectId, ImportJobState.Running, next, cancellationToken);
        return next;
    }

    private static ImportCounts Counts(Tally tally, IReadOnlyDictionary<string, int> failed)
        => new(
            new ImportEntityCount(tally.Sprints, tally.Sprints, 0),
            new ImportEntityCount(tally.Done, tally.Total, failed.GetValueOrDefault("issue")),
            new ImportEntityCount(tally.Comments, tally.Comments + failed.GetValueOrDefault("comment"), failed.GetValueOrDefault("comment")),
            new ImportEntityCount(tally.Attachments, tally.Attachments + failed.GetValueOrDefault("attachment"), failed.GetValueOrDefault("attachment")),
            new ImportEntityCount(tally.Revisions, tally.Revisions, 0));

    private async Task StopAsync(ImportJobRow job, Guid? projectId, ImportJobProgressJson progress, Tally tally, string message, CancellationToken cancellationToken)
    {
        var stopped = progress with { Message = message, Counts = Counts(tally, await rows.PendingCountsAsync(job.Id, cancellationToken)) };
        await rows.LogAsync(job.Id, ImportLogLevel.Info, message, cancellationToken);
        await rows.FinishAsync(job.Id, ImportJobState.Stopped, stopped, cancellationToken);
        await Notify(projectId, job.Id, ImportJobState.Stopped, stopped);
    }

    private async Task FinishAsync(ImportJobRow job, Guid projectId, ImportJobProgressJson progress, Tally tally, bool rerun, CancellationToken cancellationToken)
    {
        await rows.KeepNewestPendingAsync(job.Id, cancellationToken);
        var pending = await rows.PendingCountsAsync(job.Id, cancellationToken);
        var open = pending.Values.Sum();
        var state = open > 0 ? ImportJobState.DoneWithErrors : ImportJobState.Done;
        var message = open > 0 ? $"Import finished: {open} to review" : "Import finished";
        var finished = progress with
        {
            Percent = 100,
            Step = ImportStep.Finishing,
            Message = message,
            Counts = Counts(tally, pending),
            RerunResult = rerun ? new ImportRerunResult(tally.Updated, 0, tally.Added) : null,
        };
        await rows.LogAsync(job.Id, open > 0 ? ImportLogLevel.Warning : ImportLogLevel.Info, message, cancellationToken);
        await rows.FinishAsync(job.Id, state, finished, cancellationToken);
        await Notify(projectId, job.Id, state, finished);
    }

    private async Task FailAsync(ImportJobRow job, ImportJobProgressJson progress, string message)
    {
        var failed = progress with { Message = message };
        await rows.LogAsync(job.Id, ImportLogLevel.Error, message, CancellationToken.None);
        await rows.FinishAsync(job.Id, ImportJobState.Failed, failed, CancellationToken.None);
        await Notify((await rows.FindAsync(job.Id, CancellationToken.None))?.ProjectId, job.Id, ImportJobState.Failed, failed);
    }

    private async Task SaveAndNotifyAsync(Guid jobId, Guid? projectId, ImportJobState state, ImportJobProgressJson progress, CancellationToken cancellationToken)
    {
        await rows.SaveProgressAsync(jobId, progress, cancellationToken);
        await Notify(projectId, jobId, state, progress);
    }

    private Task Notify(Guid? projectId, Guid jobId, ImportJobState state, ImportJobProgressJson progress) => notifier.NotifyAsync(projectId, jobId, state, progress);

    private static bool IsImported(ImportMappingDto mapping, JiraIssue issue)
        => mapping.IssueTypes.FirstOrDefault(t => t.JiraType == issue.Type) is { Target: not ImportTypeTarget.Skip };

    private static DefaultHttpContext ActorContext(Guid userId)
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "ImportRunner")) };
}

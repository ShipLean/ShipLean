using Agoda.IoC.Core;
using ShipLean.Application.Features.Import.Apply;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Import.Jobs;

// Tries failed rows of a finished job again. IJiraClient cannot fetch one issue by key, so the project's search is paged until every wanted key has been seen. The applier
// writes a new import_error row when a row fails again; the old row is marked retried, so one failing item stays one pending row.
[RegisterPerRequest]
public sealed class ImportRetry(ImportJobRows rows, IJiraConnections connections, IJiraClient jira, IImportApplier applier, ImportErrorWriter errorWriter)
{
    public const int PageSize = 50;

    // A Jira that never reaches a last page, or sends endless issues, must not drive the server out of memory or time: a job reads at most this much.
    public const int MaxPages = 400;
    public const int MaxIssues = 20_000;

    public static void RequireWithinLimits(int pages, int issues)
    {
        if (pages > MaxPages || issues > MaxIssues)
        {
            throw ApiException.Conflict("Jira sent more issues than ShipLean imports in one job. Import a smaller Jira project.");
        }
    }

    public async Task RunAsync(ImportJobRow job, IReadOnlyList<ImportErrorRow> errors, CancellationToken cancellationToken)
    {
        var projectId = job.ProjectId ?? throw ApiException.Conflict("That import never reached its project, so there is nothing to retry. Re-run it instead.");
        var connectionId = await rows.CurrentConnectionAsync(job.Id, cancellationToken)
            ?? throw ApiException.NotFound("This project has no Jira connection any more. Save one under Integrations, then run the import again.");
        var credentials = await connections.GetCredentialsAsync(connectionId, cancellationToken);
        var mapping = await rows.ReadMappingAsync(job.Id, cancellationToken);
        var wanted = errors.GroupBy(e => e.IssueKey).ToDictionary(g => g.Key, g => g.ToList());
        var found = await FindIssuesAsync(credentials, job.JiraProjectKey, mapping, [.. wanted.Keys], cancellationToken);

        var run = applier.Begin(projectId, job.Id, credentials, mapping);
        await applier.PrepareAsync(run, [.. found.Values.SelectMany(i => i.Sprints).DistinctBy(s => s.Id)], cancellationToken);
        foreach (var warning in run.Warnings)
        {
            await rows.LogAsync(job.Id, ImportLogLevel.Warning, warning, cancellationToken);
        }

        foreach (var (key, group) in wanted)
        {
            if (!found.TryGetValue(key, out var issue))
            {
                await Gone(job, group, key, cancellationToken);
                continue;
            }

            foreach (var entity in group.Any(e => e.Entity == "issue") ? ["issue"] : group.Select(e => e.Entity).Distinct())
            {
                await RetryEntityAsync(run, issue, entity, cancellationToken);
            }

            await rows.LogAsync(job.Id, ImportLogLevel.Info, $"Retried {key}", cancellationToken);
        }

        foreach (var error in errors)
        {
            await rows.ResolveErrorAsync(error.Id, ImportErrorResolution.Retried, cancellationToken);
        }

        await RecountAsync(job, cancellationToken);
        await rows.FinishIfResolvedAsync(job.Id, cancellationToken);
    }

    private async Task RetryEntityAsync(ImportRun run, JiraIssue issue, string entity, CancellationToken cancellationToken)
    {
        if (entity == "issue")
        {
            var outcome = await applier.ApplyIssueAsync(run, issue, cancellationToken);
            if (outcome.ItemId is { } itemId)
            {
                await applier.ApplyCommentsAsync(run, issue, itemId, cancellationToken);
                await applier.ApplyAttachmentsAsync(run, issue, itemId, cancellationToken);
            }

            return;
        }

        if (!run.EarlierItemIds.TryGetValue(issue.Key, out var existing))
        {
            return;
        }

        if (entity == "comment")
        {
            await applier.ApplyCommentsAsync(run, issue, existing, cancellationToken);
        }
        else
        {
            await applier.ApplyAttachmentsAsync(run, issue, existing, cancellationToken);
        }
    }

    private async Task<Dictionary<string, JiraIssue>> FindIssuesAsync(
        JiraCredentials credentials, string jiraProjectKey, ImportMappingDto mapping, HashSet<string> wanted, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, JiraIssue>();
        string? continuation = null;
        string[] numberFields = mapping.Fields.StoryPointsFieldId.Length > 0 ? [mapping.Fields.StoryPointsFieldId] : [];
        int pages = 0, seen = 0;
        do
        {
            var page = await jira.SearchIssuesAsync(credentials, jiraProjectKey, continuation, PageSize, numberFields, cancellationToken);
            seen += page.Issues.Count;
            RequireWithinLimits(++pages, seen);
            foreach (var issue in page.Issues.Where(i => wanted.Contains(i.Key)))
            {
                found[issue.Key] = issue;
            }

            continuation = page.Continuation;
        }
        while (continuation is not null && found.Count < wanted.Count);

        return found;
    }

    // Jira no longer has the issue: the error stays pending, saying so.
    private async Task Gone(ImportJobRow job, List<ImportErrorRow> group, string key, CancellationToken cancellationToken)
    {
        var first = group[0];
        await rows.LogAsync(job.Id, ImportLogLevel.Error, $"Jira no longer has {key}", cancellationToken);
        await errorWriter.WriteAsync(job.Id, Enum.Parse<ImportStep>(first.Step, ignoreCase: true), first.Entity, first.SourceKey, $"Jira no longer has {key}.", cancellationToken);
    }

    // The failed counts follow the rows that are still pending; what is not failed is done.
    private async Task RecountAsync(ImportJobRow job, CancellationToken cancellationToken)
    {
        var progress = ImportJson.Read<ImportJobProgressJson>(job.Progress);
        var pending = await rows.PendingCountsAsync(job.Id, cancellationToken);
        var counts = progress.Counts;
        progress = progress with
        {
            Counts = counts with
            {
                Issues = Recount(counts.Issues, pending, "issue"),
                Comments = Recount(counts.Comments, pending, "comment"),
                Attachments = Recount(counts.Attachments, pending, "attachment"),
            },
        };
        await rows.SaveProgressAsync(job.Id, progress, cancellationToken);
    }

    private static ImportEntityCount Recount(ImportEntityCount count, IReadOnlyDictionary<string, int> pending, string entity)
    {
        var failed = pending.GetValueOrDefault(entity);
        return count with { Failed = failed, Done = Math.Max(0, count.Total - failed) };
    }
}

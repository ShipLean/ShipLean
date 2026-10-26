using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Planning.Reports;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Work;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class ReportTools(
    ItemResolver items,
    IAgentAddresses addresses,
    IPlanReads plans,
    IWorkItemStore store,
    IStateCatalog catalog,
    ICommentStore comments,
    ILinkCards linkCards,
    IWorkItemTreeWriter treeWriter,
    StaleWrites writes,
    ICurrentAgent agent)
{
    [McpServerTool(Name = "report_task"), McpScope(McpScopes.Write)]
    [Description("Hand in the report for a task of a plan when you stop work on it, in the shape its brief asks for. The report lands as a comment on the task, each commit and the pull request become links on it, and the task moves on. Done moves it to the first Completed state. Blocked unassigns it and returns it to the first Proposed state, with your reason. Partial leaves it as it is, or moves it to the first InProgress state if it has not started or was Completed. Each left_undone item becomes an orphan task. A report on a Completed task is accepted: done leaves it there, blocked and partial reopen it. Send it once per stop, done or not. At most 25 commits and 25 left_undone items a call.")]
    public async Task<CallToolResult> ReportTask(
        [Description("The task's key, such as SHIP-12.")] string item,
        [Description("done, blocked or partial.")] string status,
        [Description("The commits you pushed, in order. Left out means none.")] IReadOnlyList<ReportCommit>? commits = null,
        [Description("The pull request's URL, when there is one.")] string? pr_url = null,
        [Description("Each test command you ran, with its pass and fail counts and how long it took. Left out means none.")] IReadOnlyList<string>? tests_run = null,
        [Description("The acceptance tests that define done, each with pass or fail. Left out means none.")] IReadOnlyList<string>? acceptance_tests = null,
        [Description("Config files you changed and why.")] string? config_files_changed = null,
        [Description("Red tests, each with the task that owns it.")] string? red_tests = null,
        [Description("About how much context you used, in thousands of tokens.")] int? context_used_k = null,
        [Description("The types, tools and signatures you added that others build on.")] string? public_surface = null,
        [Description("Decisions a later task would otherwise have to rediscover.")] string? decisions = null,
        [Description("What cost you time.")] string? gotchas = null,
        [Description("What you left undone, each with why. Left out means none.")] IReadOnlyList<string>? left_undone = null,
        [Description("Temporary behaviour you left in, what it stands in for and what replaces it.")] string? temporary_behaviour = null,
        [Description("A constraint of the brief you could not meet, and why.")] string? constraint_not_met = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var projectKey = PlanningReply.ProjectKeyOf(resolved.Key);
        var parsedStatus = ReportChecks.ParseStatus(status);
        var commitList = commits ?? [];
        var undone = ReportChecks.LeftUndone(left_undone);
        ReportChecks.CheckLinks(commitList, pr_url);
        ReportChecks.CheckLists(tests_run, acceptance_tests);
        var blockedReason = parsedStatus == ReportStatus.Blocked ? ReportChecks.BlockedReason(constraint_not_met, undone) : null;

        var (tree, taskId) = await plans.TreeOfAsync(resolved.Id, cancellationToken);
        var story = tree.Stories.FirstOrDefault(s => s.Tasks.Any(t => t.Id == taskId))
            ?? throw ApiException.Validation($"{resolved.Key} is not a task of a plan, and only a task can be reported.", "item");
        var task = story.Tasks.Single(t => t.Id == taskId);
        var snapshot = await store.GetAsync(taskId, cancellationToken);
        var allStates = await catalog.ListAsync(snapshot.ProjectId, cancellationToken);
        var category = WorkStates.CategoryOf(allStates, snapshot.StateId);
        if (category == StateCategory.Removed)
        {
            throw ApiException.Validation($"{resolved.Key} is {category.ToString().ToLowerInvariant()}, so it takes no reports.", "item");
        }

        var fields = new ReportFields(
            tests_run ?? [], acceptance_tests ?? [], config_files_changed, red_tests, context_used_k, public_surface, decisions, gotchas, undone, temporary_behaviour, constraint_not_met);
        var report = ReportBody.Build(parsedStatus, blockedReason, commitList, pr_url, fields);
        if (report.Length > MarkdownRenderer.MaxLength)
        {
            throw ApiException.Validation($"The report is too long: {report.Length:N0} characters, the limit is {MarkdownRenderer.MaxLength:N0}.");
        }

        // Links, orphans, the comment, then the state move: a call that fails part-way can be sent again.
        var linksAdded = await AddLinksAsync(taskId, commitList, pr_url, cancellationToken);
        var orphans = await AddOrphansAsync(snapshot.ProjectId, story, task, undone, cancellationToken);
        var added = await comments.AddAsync(taskId, new CommentCreate(report), cancellationToken);
        await SupersedeEarlierReportsAsync(taskId, added.Id, cancellationToken);

        var changes = StateMove(parsedStatus, snapshot, allStates, category);
        if (changes is not null)
        {
            await writes.PatchAsync(snapshot.Id, snapshot.Key, changes, snapshot.Revision, false, cancellationToken);
        }
        var after = changes is null ? snapshot : await store.GetAsync(taskId, cancellationToken);

        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var result = new ReportResult(
            projectKey, McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key), resolved.Key, allStates.Single(s => s.Id == after.StateId).Name, added.Id, linksAdded, orphans);
        return PlanningReply.Of(result, $"Report for {resolved.Key} received; it is now {result.State}.");
    }

    // Each commit and the pull request become a link titled by PlanMarkers; an address the task already has, or that this call repeats, is skipped.
    // Saved without reading the address: up to 26 reads in one request would hold it for minutes. The cards' refresh reads them later.
    private async Task<int> AddLinksAsync(Guid taskId, IReadOnlyList<ReportCommit> commits, string? prUrl, CancellationToken cancellationToken)
    {
        var existing = await linkCards.ListAsync(taskId, cancellationToken);
        var seen = existing.Code.Concat(existing.Pages).Select(l => l.Url).ToHashSet(StringComparer.Ordinal);
        var wanted = commits.Select(c => (Url: c.Url!.Trim(), Title: PlanMarkers.Commit(c.Sha!.Trim(), c.Subject ?? string.Empty)))
            .Concat(prUrl is null ? [] : [(Url: prUrl.Trim(), Title: PlanMarkers.PullRequest(prUrl))]);

        var added = 0;
        foreach (var (url, title) in wanted)
        {
            if (seen.Add(url))
            {
                await linkCards.AddUrlAsync(taskId, url, title, null, AgentStamp.Of(agent), readPreview: false, cancellationToken: cancellationToken);
                added++;
            }
        }
        return added;
    }

    // One orphan task per left-undone item under the task's story, tagged with the task's wave, lane and model. An item the story
    // already has as a live orphan with that exact title is not made again.
    private async Task<IReadOnlyList<OrphanTask>> AddOrphansAsync(Guid projectId, PlanStory story, PlanTask task, IReadOnlyList<string> undone, CancellationToken cancellationToken)
    {
        var tags = PlanTags.Format(new PlanTaskTags(story.Wave, story.Lane, task.Tags.Model, Orphan: true));
        var missing = undone.Where(title => !story.Tasks.Any(t => t.Orphan && t.Title == title)).ToList();
        var created = missing.Count == 0
            ? null
            : await treeWriter.CreateTreeAsync(
                new WorkItemTreeCreate(
                    projectId,
                    story.Id,
                    [.. missing.Select(title => new WorkItemTreeCreate.Node(
                        WorkItemTypeId.Task,
                        title,
                        new WorkItemChanges { Tags = new FieldSet<IReadOnlyList<string>>(tags), Description = new FieldSet<string>($"Left undone by {task.Key}.") },
                        []))]),
                WriteOptions.User,
                cancellationToken);

        return
        [
            .. undone.Select(title => story.Tasks.FirstOrDefault(t => t.Orphan && t.Title == title) is { } existing
                ? new OrphanTask(existing.Key, existing.Title)
                : new OrphanTask(created!.Items[missing.IndexOf(title)].Key, title)),
        ];
    }

    // The newest report is the task's current one; the earlier live ones stay in the Log, marked superseded.
    private async Task SupersedeEarlierReportsAsync(Guid taskId, Guid newest, CancellationToken cancellationToken)
    {
        var earlier = (await comments.ListAsync(taskId, cancellationToken))
            .Where(c => c.Id != newest && c.Superseded is null && PlanMarkers.IsReport(c.Body));
        foreach (var comment in earlier)
        {
            await comments.SupersedeAsync(comment.Id, cancellationToken);
        }
    }

    // Done goes to the first Completed state (a task already Completed stays where it is). Blocked unassigns and returns to the first Proposed state. Partial stays, or starts (also reopens a Completed task).
    private static WorkItemChanges? StateMove(ReportStatus status, WorkItemSnapshot snapshot, IReadOnlyList<StateDto> allStates, StateCategory category)
    {
        var target = status switch
        {
            ReportStatus.Done => category == StateCategory.Completed ? null : WorkStates.First(allStates, snapshot.Type, StateCategory.Completed),
            ReportStatus.Blocked => WorkStates.First(allStates, snapshot.Type, StateCategory.Proposed),
            _ => category is StateCategory.Proposed or StateCategory.Completed ? WorkStates.First(allStates, snapshot.Type, StateCategory.InProgress) : null,
        };
        var changes = new WorkItemChanges
        {
            State = target is null || target.Id == snapshot.StateId ? null : new FieldSet<Guid>(target.Id),
            Assignee = status == ReportStatus.Blocked && snapshot.AssigneeId is not null ? new FieldSet<Guid?>(null) : null,
        };
        return changes is { State: null, Assignee: null } ? null : changes;
    }
}

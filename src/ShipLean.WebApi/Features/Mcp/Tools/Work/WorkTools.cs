using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Notes;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

// "I'm on it": find work nobody has taken, take it, report progress, give it back. Work is pulled, never pre-assigned.
[McpServerToolType]
internal sealed class WorkTools(
    ProjectResolver projects,
    ItemResolver items,
    SprintResolver sprints,
    StateResolver states,
    IWorkTreeReads tree,
    IWorkItemStore store,
    IStateCatalog catalog,
    StaleWrites writes,
    NoteWriter notes,
    ICurrentUser currentUser,
    AgentBaseUrl baseUrls,
    IServiceProvider services)
{
    [McpServerTool(Name = "find_work_to_pick_up"), McpScope(McpScopes.Read)]
    [Description("List work nobody has taken yet in a sprint (the current sprint unless you name one), top of the stack first. Only unassigned items that have not been started. Optionally only one type (epic, feature, story, bug, task) or only what sits under a parent item such as a story or feature.")]
    public async Task<CallToolResult> FindWorkToPickUp(
        [Description("Project key such as SHIP. Leave out for the person's most recent project.")] string? project = null,
        [Description("current, next or a sprint name. Defaults to current.")] string? sprint = null,
        [Description("Only this type: epic, feature, story, bug or task.")] string? type = null,
        [Description("Only items under this parent, by key such as SHIP-12.")] string? parent = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProject = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        var resolvedSprint = await sprints.ResolveAsync(resolvedProject.Id, sprint ?? "current", cancellationToken: cancellationToken);
        var wantedType = ParseType(type);
        Guid? parentId = null;
        if (parent is not null)
        {
            parentId = (await items.ResolveAsync(parent, ProjectRole.Member, resolvedProject.Id, "parent", cancellationToken)).Id;
        }

        var ready = await tree.ReadyAsync(resolvedProject.Id, resolvedSprint.Id, wantedType, parentId, cancellationToken);
        var baseUrl = await BaseUrlAsync(cancellationToken);
        var result = new FindWorkResult(
            resolvedProject.Key,
            McpOpenUrls.Taskboard(baseUrl, resolvedProject.Key, resolvedSprint.Id),
            resolvedSprint.Name,
            [.. ready.Select(i => new ReadyItem(i.Key, i.Title, i.Type.Name, i.State.Name, i.Points, McpOpenUrls.WorkItem(baseUrl, resolvedProject.Key, i.Key)))]);
        return WorkReply.Of(result, $"{result.Items.Count} ready in {result.Sprint}: {string.Join(", ", result.Items.Select(i => i.Key))}.");
    }

    [McpServerTool(Name = "pick_up"), McpScope(McpScopes.Write)]
    [Description("Take a work item: assign it to the person and start it, moving it to the first in-progress state of its type, like the Pick up button on the taskboard. Refuses work someone else already has or that is already finished.")]
    public async Task<CallToolResult> PickUp(
        [Description("The work item key, such as SHIP-123.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var allStates = await catalog.ListAsync(snapshot.ProjectId, cancellationToken);
        var category = WorkStates.CategoryOf(allStates, snapshot.StateId);

        if (category is StateCategory.Resolved or StateCategory.Completed or StateCategory.Removed)
        {
            throw ApiException.Validation($"{resolved.Key} is already {category.ToString().ToLowerInvariant()}, so there is nothing to pick up.", "item");
        }
        if (snapshot.AssigneeId is { } owner && owner != currentUser.Id)
        {
            throw ApiException.Conflict($"{resolved.Key} is already assigned to someone else. Work is pulled, not taken over.");
        }

        var changes = new WorkItemChanges
        {
            Assignee = snapshot.AssigneeId == currentUser.Id ? null : new FieldSet<Guid?>(currentUser.Id),
            State = category == StateCategory.Proposed ? new FieldSet<Guid>(WorkStates.First(allStates, snapshot.Type, StateCategory.InProgress).Id) : null,
        };
        var written = changes is { Assignee: null, State: null }
            ? null
            : await writes.PatchAsync(snapshot.Id, snapshot.Key, changes, snapshot.Revision, false, cancellationToken);

        var after = written is null ? snapshot : await store.GetAsync(resolved.Id, cancellationToken);
        var result = new PickUpResult(
            ProjectKey(resolved.Key),
            McpOpenUrls.WorkItem(await BaseUrlAsync(cancellationToken), ProjectKey(resolved.Key), resolved.Key),
            resolved.Key,
            after.Title,
            allStates.Single(s => s.Id == after.StateId).Name,
            after.AssigneeId == currentUser.Id,
            after.Revision,
            written?.Notices ?? []);
        return WorkReply.Of(result, $"{result.Key} is yours and now {result.State}.");
    }

    [McpServerTool(Name = "update_progress"), McpScope(McpScopes.Write)]
    [Description("Report progress on a work item in one call, for example when a PR is up: move it to a state by name, add a note, add a link (a URL and a title) and adjust its points. Give at least one. When this finishes the last open task of a story, the answer says so and offers the state to move the story to; it does not move the story itself.")]
    public async Task<CallToolResult> UpdateProgress(
        [Description("The work item key, such as SHIP-123.")] string item,
        [Description("Move to this state, by name (for example In review or Closed). States differ per work item type.")] string? state = null,
        [Description("A comment to leave on the item, written after the change.")] string? note = null,
        [Description("A link to attach, for example the pull request URL (http or https).")] string? link_url = null,
        [Description("The link's title. Defaults to the URL.")] string? link_title = null,
        [Description("New story points.")] decimal? points = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var wantsNote = !string.IsNullOrWhiteSpace(note);
        if (string.IsNullOrWhiteSpace(state) && !wantsNote && link_url is null && points is null)
        {
            throw ApiException.Validation("Say what to update: a state, a note, a link or points.", "state");
        }
        if (link_url is not null && !(Uri.TryCreate(link_url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
        {
            throw ApiException.Validation("The link must be an http or https URL.", "link_url");
        }

        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var target = string.IsNullOrWhiteSpace(state) ? null : await states.ResolveAsync(snapshot.ProjectId, snapshot.Type, state, cancellationToken: cancellationToken);
        if (target?.Category == StateCategory.Removed)
        {
            throw ApiException.Validation("Removing work is not something update_progress does.", "state");
        }

        var linkTitle = string.IsNullOrWhiteSpace(link_title) ? link_url : link_title;
        var changes = new WorkItemChanges
        {
            State = target is { } t && t.Id != snapshot.StateId ? new FieldSet<Guid>(t.Id) : null,
            Points = points is { } p && p != snapshot.Points ? new FieldSet<decimal?>(p) : null,
            AddLinks = link_url is null ? [] : [new LinkValue(LinkType.Url, null, null, link_url, linkTitle)],
        };
        var written = changes is { State: null, Points: null, AddLinks.Count: 0 }
            ? null
            : await writes.PatchAsync(snapshot.Id, snapshot.Key, changes, snapshot.Revision, false, cancellationToken);

        var noteOutcome = wantsNote ? await notes.AddAsync(snapshot.Id, note!, cancellationToken) : null;
        var allStates = await catalog.ListAsync(snapshot.ProjectId, cancellationToken);
        var (lastTaskClosed, parentKey, moveParentTo) = changes.State is not null && target?.Category == StateCategory.Completed
            ? await ClosedLastTaskAsync(snapshot, allStates, cancellationToken)
            : (false, null, null);

        var after = written is null ? snapshot : await store.GetAsync(resolved.Id, cancellationToken);
        var projectKey = ProjectKey(resolved.Key);
        var result = new UpdateProgressResult(
            projectKey,
            McpOpenUrls.WorkItem(await BaseUrlAsync(cancellationToken), projectKey, resolved.Key),
            resolved.Key,
            allStates.Single(s => s.Id == after.StateId).Name,
            after.Points,
            after.Revision,
            changes.AddLinks.Count > 0,
            noteOutcome?.Added,
            noteOutcome?.Error,
            lastTaskClosed,
            parentKey,
            moveParentTo);
        return WorkReply.Of(result, $"{result.Key} is {result.State}." + (result.MoveParentTo is { } parentState ? $" That was the last open task: {result.ParentKey} can move to {parentState}." : string.Empty));
    }

    [McpServerTool(Name = "hand_back"), McpScope(McpScopes.Write)]
    [Description("Give a work item back to the team: unassign it and return it to the first not-started state of its type, with a note saying why or how far it got.")]
    public async Task<CallToolResult> HandBack(
        [Description("The work item key, such as SHIP-123.")] string item,
        [Description("Why it is being handed back, or where it got to. Written as a comment after the change.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var allStates = await catalog.ListAsync(snapshot.ProjectId, cancellationToken);
        var category = WorkStates.CategoryOf(allStates, snapshot.StateId);
        if (category is StateCategory.Completed or StateCategory.Removed)
        {
            throw ApiException.Validation($"{resolved.Key} is {category.ToString().ToLowerInvariant()}, so there is nothing to hand back.", "item");
        }

        var firstProposed = WorkStates.First(allStates, snapshot.Type, StateCategory.Proposed);
        var changes = new WorkItemChanges
        {
            Assignee = snapshot.AssigneeId is null ? null : new FieldSet<Guid?>(null),
            State = snapshot.StateId == firstProposed.Id ? null : new FieldSet<Guid>(firstProposed.Id),
        };
        var written = changes is { Assignee: null, State: null }
            ? null
            : await writes.PatchAsync(snapshot.Id, snapshot.Key, changes, snapshot.Revision, false, cancellationToken);

        var noteOutcome = string.IsNullOrWhiteSpace(note) ? null : await notes.AddAsync(snapshot.Id, note, cancellationToken);
        var after = written is null ? snapshot : await store.GetAsync(resolved.Id, cancellationToken);
        var projectKey = ProjectKey(resolved.Key);
        var result = new HandBackResult(
            projectKey,
            McpOpenUrls.WorkItem(await BaseUrlAsync(cancellationToken), projectKey, resolved.Key),
            resolved.Key,
            allStates.Single(s => s.Id == after.StateId).Name,
            after.Revision,
            noteOutcome?.Added,
            noteOutcome?.Error);
        return WorkReply.Of(result, $"{result.Key} is back in {result.State}, unassigned.");
    }

    // After closing a task: when it was the last open task of its story or bug, name the parent and the state to offer.
    private async Task<(bool Last, string? ParentKey, string? MoveParentTo)> ClosedLastTaskAsync(WorkItemSnapshot task, IReadOnlyList<StateDto> allStates, CancellationToken cancellationToken)
    {
        if (task.Type != WorkItemTypeId.Task || task.ParentId is not { } parentId || task.ParentType is not (WorkItemTypeId.Story or WorkItemTypeId.Bug))
        {
            return (false, null, null);
        }

        var siblings = await tree.DescendantsAsync(task.ProjectId, parentId, WorkItemTypeId.Task, cancellationToken);
        if (siblings.Any(s => s.State.Category != StateCategory.Completed))
        {
            return (false, null, null);
        }

        var parent = await store.GetAsync(parentId, cancellationToken);
        var offer = WorkStates.CategoryOf(allStates, parent.StateId) is StateCategory.Proposed or StateCategory.InProgress
            ? WorkStates.FirstOrDefault(allStates, parent.Type, StateCategory.Resolved)
            : null;
        return (true, parent.Key, offer?.Name);
    }

    private static WorkItemTypeId? ParseType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "epic" => WorkItemTypeId.Epic,
            "feature" => WorkItemTypeId.Feature,
            "story" => WorkItemTypeId.Story,
            "bug" => WorkItemTypeId.Bug,
            "task" => WorkItemTypeId.Task,
            _ => throw ApiException.Validation($"\"{type}\" is not a work item type. Use epic, feature, story, bug or task.", "type"),
        };
    }

    private static string ProjectKey(string itemKey) => itemKey[..itemKey.LastIndexOf('-')];

    // The server's own address for open_url: the request's, through the accessor (this is a tool, not a request handler).
    private async Task<string> BaseUrlAsync(CancellationToken cancellationToken)
        => (await baseUrls.ReadAsync(services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request, cancellationToken)).Issuer;
}

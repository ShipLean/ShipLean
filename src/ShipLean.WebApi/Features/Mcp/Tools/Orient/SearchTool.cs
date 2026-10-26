using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Criteria;
using ShipLean.Application.Features.Mcp.Orient;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Search;
using ShipLean.Contracts.Refs;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

[McpServerToolType]
internal sealed class SearchTool(
    ProjectResolver projects,
    IProjectAccess access,
    StateResolver states,
    MemberResolver members,
    SprintResolver sprints,
    ItemResolver items,
    OrientReads reads,
    IAgentAddresses addresses,
    ISearchRanker ranker)
{
    private const int DefaultWordsLimit = 10;

    [McpServerTool(Name = "search"), McpScope(McpScopes.Read)]
    [Description("Find work items, sprints and projects by words and/or by type, state, assignee, sprint, tag or parent. Words are matched by meaning as well as spelling, in titles, descriptions, tags, comments and Markdown attachments (named in `attachment`), and a work item key ranks first. Use this before create_feature when an idea might already have a Feature. Best matches first; at most `limit` (default 10 with words, 25 without; max 25), and it says when there are more. Removed items are left out. " +
        "Without `project` the words search every project you are in (it used to default to your most recent project; give `project` to narrow it). With words, `state`, `assignee` and `sprint` only make sense inside one project, so give `project` or a `parent` key with them. `type` and `tag` work across projects. " +
        "With words, closed items are included unless `include_closed` is false. Without words it lists the filtered items, newest change first, in your most recent project when none is named.")]
    public async Task<CallToolResult> SearchAsync(
        [Description("Words to look for, like \"checkout timeout\" (optional when you filter).")] string? words = null,
        [Description("Project key (optional: with words, every project you are in).")] string? project = null,
        [Description("Work item type: epic, feature, story, bug or task.")] string? type = null,
        [Description("A state name, like \"In progress\" (with words, needs project or parent).")] string? state = null,
        [Description("A person: me, a name or an email (with words, needs project or parent).")] string? assignee = null,
        [Description("A sprint: current, next or its name (with words, needs project or parent).")] string? sprint = null,
        [Description("A tag.")] string? tag = null,
        [Description("The key of the parent item, like SHIP-12. It implies its project.")] string? parent = null,
        [Description("Include items in a completed state when searching by words (default true).")] bool include_closed = true,
        [Description("How many matches to return (default 10 with words, 25 without; at most 25).")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var text = string.Join(' ', (words ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var take = Math.Clamp(limit ?? (text.Length == 0 ? OrientReads.SearchCap : DefaultWordsLimit), 1, OrientReads.SearchCap);
        var wantedType = string.IsNullOrWhiteSpace(type) ? (WorkItemTypeId?)null : ParseType(type);
        var hasState = !string.IsNullOrWhiteSpace(state);
        var hasAssignee = !string.IsNullOrWhiteSpace(assignee);
        var hasSprint = !string.IsNullOrWhiteSpace(sprint);
        var hasTag = !string.IsNullOrWhiteSpace(tag);
        var hasParent = !string.IsNullOrWhiteSpace(parent);
        if (text.Length == 0 && wantedType is null && !hasState && !hasAssignee && !hasSprint && !hasTag && !hasParent)
        {
            throw ApiException.Validation("Give some words or at least one filter to search by.", "words");
        }

        ResolvedProject? named = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            named = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
            await access.RequireAsync(named.Id, ProjectRole.Member, cancellationToken);
        }
        var parentItem = hasParent ? await items.ResolveAsync(parent!, ProjectRole.Member, named?.Id, "parent", cancellationToken) : null;
        var projectId = named?.Id ?? parentItem?.ProjectId;
        if (text.Length > 0 && projectId is null && (hasState || hasAssignee || hasSprint))
        {
            var yours = string.Join(", ", (await reads.ProjectsAsync(null, cancellationToken)).Select(p => p.Key));
            throw ApiException.Validation($"A state, assignee or sprint filter works inside one project: give `project` (one of {yours}) or a `parent` key.", "project");
        }

        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var projectKey = named?.Key ?? (projectId is { } implied ? (await reads.ProjectsAsync(implied, cancellationToken)).Single().Key : null);
        return text.Length == 0
            ? await FilterOnlyAsync(named, projectId, projectKey, parentItem, wantedType, state, assignee, sprint, tag, take, issuer, cancellationToken)
            : await RankedAsync(text, projectId, projectKey, parentItem, wantedType, state, assignee, sprint, tag, include_closed, take, issuer, cancellationToken);
    }

    // Words go to the ranker. Naming a state includes closed items, because the named state may be a completed one.
    private async Task<CallToolResult> RankedAsync(
        string text, Guid? projectId, string? projectKey, ResolvedItem? parentItem, WorkItemTypeId? wantedType, string? state, string? assignee, string? sprint, string? tag,
        bool includeClosed, int limit, string issuer, CancellationToken cancellationToken)
    {
        CriteriaJson? criteria = null;
        if (projectId is { } id)
        {
            var clauses = new List<CriteriaClause>();
            if (!string.IsNullOrWhiteSpace(state))
            {
                clauses.Add(CriteriaClauses.Of(FieldIds.State, CriteriaOp.In, await StateIdsAsync(id, wantedType, state, cancellationToken)));
            }
            if (!string.IsNullOrWhiteSpace(assignee))
            {
                clauses.Add(CriteriaClauses.Of(FieldIds.Assignee, CriteriaOp.Equal, (await members.ResolveAsync(id, assignee, "assignee", cancellationToken)).Id.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(sprint))
            {
                clauses.Add(CriteriaClauses.Of(FieldIds.Sprint, CriteriaOp.Equal, (await sprints.ResolveAsync(id, sprint, "sprint", cancellationToken)).Id.ToString()));
            }
            if (parentItem is not null)
            {
                clauses.Add(CriteriaClauses.Of(FieldIds.Parent, CriteriaOp.Equal, parentItem.Id.ToString()));
            }
            criteria = clauses.Count == 0 ? null : new CriteriaJson(CriteriaMatch.All, clauses);
        }

        var results = await ranker.SearchAsync(
            new SearchQuery(
                text,
                projectId is { } only ? [only] : null,
                wantedType is { } t ? [t] : null,
                string.IsNullOrWhiteSpace(tag) ? null : [tag.Trim()],
                includeClosed || !string.IsNullOrWhiteSpace(state),
                Limit: limit,
                Criteria: criteria),
            cancellationToken);

        var matches = new List<SearchResult.Match>();
        matches.AddRange(results.Items.Select(i => new SearchResult.Match(
            "item", i.Title, i.ProjectKey, McpOpenUrls.WorkItem(issuer, i.ProjectKey, i.Key),
            i.Key, i.Type.ToString(), i.State, i.ParentPath.LastOrDefault(), i.Assignee, i.Sprint, i.Points, i.Tags,
            MatchedIn: i.MatchedIn.ToString().ToLowerInvariant(), Snippet: i.Snippet, Attachment: i.Attachment)));
        matches.AddRange(results.Sprints.Select(s => new SearchResult.Match(
            "sprint", string.IsNullOrWhiteSpace(s.Goal) ? s.Name : $"{s.Name}: {s.Goal}", s.ProjectKey, McpOpenUrls.Taskboard(issuer, s.ProjectKey, s.Id),
            Goal: s.Goal, Dates: $"{s.StartDate:yyyy-MM-dd} to {s.EndDate:yyyy-MM-dd}")));
        matches.AddRange(results.Projects.Select(p => new SearchResult.Match(
            "project", p.Name, p.Key, McpOpenUrls.Backlog(issuer, p.Key, "stories"), Key: p.Key)));

        var result = new SearchResult(
            projectKey, projectKey is null ? issuer : McpOpenUrls.Backlog(issuer, projectKey, "stories"), results.Semantic, matches, results.More);
        return OrientReply.Of(result, $"{matches.Count} match{(matches.Count == 1 ? string.Empty : "es")}{(projectKey is null ? string.Empty : $" in {projectKey}")}{(results.More ? ", more beyond these" : string.Empty)}.");
    }

    // No words: today's filter search over one project, newest change first.
    private async Task<CallToolResult> FilterOnlyAsync(
        ResolvedProject? named, Guid? projectId, string? projectKey, ResolvedItem? parentItem, WorkItemTypeId? wantedType, string? state, string? assignee, string? sprint, string? tag, int limit, string issuer,
        CancellationToken cancellationToken)
    {
        var resolved = named ?? (projectId is { } implied
            ? new ResolvedProject(implied, projectKey!, string.Empty, ProjectRole.Member)
            : await projects.ResolveAsync(null, forWrite: false, cancellationToken));
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var hasState = !string.IsNullOrWhiteSpace(state);
        var typedState = hasState && wantedType is { } stateType ? await states.ResolveAsync(resolved.Id, stateType, state!, cancellationToken: cancellationToken) : null;
        var filter = new SearchFilter(
            resolved.Id,
            [],
            wantedType,
            hasState && typedState is null ? state : null,
            string.IsNullOrWhiteSpace(assignee) ? null : (await members.ResolveAsync(resolved.Id, assignee, "assignee", cancellationToken)).Id,
            string.IsNullOrWhiteSpace(sprint) ? null : (await sprints.ResolveAsync(resolved.Id, sprint, "sprint", cancellationToken)).Id,
            string.IsNullOrWhiteSpace(tag) ? null : tag,
            parentItem?.Id,
            typedState?.Id);

        var page = await reads.SearchAsync(filter, cancellationToken);
        var shown = page.Hits.Take(limit).ToList();
        var result = new SearchResult(
            resolved.Key,
            McpOpenUrls.Backlog(issuer, resolved.Key, "stories"),
            false,
            [.. shown.Select(h => new SearchResult.Match(
                "item", h.Title, resolved.Key, McpOpenUrls.WorkItem(issuer, resolved.Key, h.Key),
                h.Key, Enum.Parse<WorkItemTypeId>(h.Type, true).ToString(), h.State, h.Parent, h.Assignee, h.Sprint, h.Points, h.Tags, Attachment: h.AttachmentFile))],
            page.More || page.Hits.Count > shown.Count);
        return OrientReply.Of(result, $"{result.Matches.Count} match{(result.Matches.Count == 1 ? string.Empty : "es")} in {resolved.Key}{(result.More ? ", more beyond these" : string.Empty)}.");
    }

    // With a type the state is that type's state; without one the name matches the states of every type.
    private async Task<string[]> StateIdsAsync(Guid projectId, WorkItemTypeId? type, string state, CancellationToken cancellationToken)
        => type is { } t
            ? [(await states.ResolveAsync(projectId, t, state, cancellationToken: cancellationToken)).Id.ToString()]
            : await reads.StateIdsByNameAsync(projectId, state, cancellationToken);

    private static WorkItemTypeId ParseType(string type)
    {
        var wanted = type.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (wanted.Equals("userstory", StringComparison.OrdinalIgnoreCase))
        {
            return WorkItemTypeId.Story;
        }
        return Enum.TryParse<WorkItemTypeId>(wanted, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw ApiException.Validation($"\"{type}\" is not a work item type: use epic, feature, story, bug or task.", "type");
    }
}

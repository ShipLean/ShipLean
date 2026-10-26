using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Orient;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Tools.Feature;

// The Feature editor's two writes: the status line (which stamps it verified) and a new Feature with its goal.
[McpServerToolType]
internal sealed class FeatureTools(
    ProjectResolver projects, ItemResolver itemResolver, StateResolver states, IProjectAccess access, IWorkItemStore store,
    StaleWrites writes, OrientReads reads, CaptureSupport support, TimeProvider clock)
{
    private const string SetStatusVia = "set_status";
    private const string CreateFeatureVia = "create_feature";

    [McpServerTool(Name = "set_status"), McpScope(McpScopes.Write)]
    [Description("Write a Feature's one-line status (up to 500 characters) and stamp it verified now, by you and through this tool, even when the text is unchanged. Call this at the end of every session on a Feature, even when nothing changed, so it shows the Feature was checked. Optionally move the Feature to a state by name in the same write. Only a Feature has a status.")]
    public async Task<CallToolResult> SetStatus(
        [Description("The Feature's key, such as SHIP-12.")] string feature,
        [Description("The status, one line.")] string status,
        [Description("Move the Feature to this state, by name, in the same write.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(feature, ProjectRole.Member, field: "feature", cancellationToken: cancellationToken);
        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var target = string.IsNullOrWhiteSpace(state) ? null : await states.ResolveAsync(snapshot.ProjectId, snapshot.Type, state, cancellationToken: cancellationToken);

        var changes = new WorkItemChanges
        {
            StatusLine = new FieldSet<string>(status ?? string.Empty),
            State = target is { } t && t.Id != snapshot.StateId ? new FieldSet<Guid>(t.Id) : null,
        };
        var written = await writes.PatchAsync(resolved.Id, resolved.Key, changes, snapshot.Revision, false, SetStatusVia, cancellationToken);

        var after = await store.GetAsync(resolved.Id, cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var names = await reads.NamesAsync(resolved.Id, after.StatusVerifiedBy is { } by ? [by] : [], cancellationToken);
        var result = new SetStatusResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key,
            after.StatusLine, names.StateName, VerifiedLine.Of(after, ByName(names, after), clock), written.Revision);
        return CaptureSupport.Reply(result, $"{resolved.Key} status is \"{result.Status}\", verified {result.Verified?.Label}, revision {result.Revision}.");
    }

    [McpServerTool(Name = "create_feature"), McpScope(McpScopes.Write)]
    [Description("Create a Feature when an idea first comes up, before any research: search first, then use this with a goal. Optionally set its one-line status, state, epic and slug. The slug is its short name in addresses; it defaults to one made from the title and can't be changed later. Name the project when the user is in more than one.")]
    public async Task<CallToolResult> CreateFeature(
        [Description("The Feature's title.")] string title,
        [Description("The goal: what the Feature is for.")] string goal,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The key of the epic this Feature belongs to.")] string? epic = null,
        [Description("Lower-case words joined by hyphens, at most 64 characters. Defaults to one made from the title.")] string? slug = null,
        [Description("The one-line status. Stamps it verified now, by you.")] string? status = null,
        [Description("Start the Feature in this state, by name.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw ApiException.Validation("A Feature needs a title.", "title");
        }

        var parent = string.IsNullOrWhiteSpace(epic) ? null : await itemResolver.ResolveAsync(epic, ProjectRole.Member, resolved.Id, "epic", cancellationToken);
        var target = string.IsNullOrWhiteSpace(state) ? null : await states.ResolveAsync(resolved.Id, WorkItemTypeId.Feature, state, cancellationToken: cancellationToken);
        var hasStatus = !string.IsNullOrWhiteSpace(status);

        var create = new WorkItemCreate(
            resolved.Id, WorkItemTypeId.Feature, title.Trim(), parent?.Id, null,
            target is null ? null : new WorkItemChanges { State = new FieldSet<Guid>(target.Id) })
        {
            Goal = goal ?? string.Empty,
            StatusLine = hasStatus ? status! : string.Empty,
            Slug = slug ?? (Slugs.FromTitle(title) is { Length: > 0 } suggested ? suggested : null),
        };
        var outcome = await store.CreateAsync(create, hasStatus ? WriteOptions.User with { Via = CreateFeatureVia } : WriteOptions.User, cancellationToken);
        var written = outcome.Written!;

        var after = await store.GetAsync(written.Id, cancellationToken);
        var names = await reads.NamesAsync(written.Id, after.StatusVerifiedBy is { } by ? [by] : [], cancellationToken);
        var result = new CreateFeatureResult(
            resolved.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), resolved.Key, written.Key), written.Key,
            after.Title, after.Slug, names.StateName, after.StatusLine, VerifiedLine.Of(after, ByName(names, after), clock), written.Revision);
        return CaptureSupport.Reply(result, $"Created Feature {written.Key} \"{result.Title}\" with slug {result.Slug}.");
    }

    private static string? ByName(ItemNames names, WorkItemSnapshot after)
        => after.StatusVerifiedBy is { } who && names.Users.TryGetValue(who, out var name) ? name : null;
}

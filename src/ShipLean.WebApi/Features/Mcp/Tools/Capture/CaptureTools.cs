using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

[McpServerToolType]
internal sealed class CaptureTools(
    ProjectResolver projects, ItemResolver itemResolver, IProjectAccess access, IWorkItemStore store, PlanEnvelope envelope, CaptureSupport support, PlanRecheck recheck)
{
    private const string PlanKind = "capture_work";

    [McpServerTool(Name = "capture_work"), McpScope(McpScopes.Write, PreviewScope = McpScopes.Read, PreviewWhenManyOf = "items")]
    [Description("Put new work in the backlog: up to 25 epics, features, stories, bugs or tasks from the conversation, each with its title, description, acceptance criteria, repro steps, severity, points, tags, priority, parent and sprint. One item is created straight away. Several items come back as a preview and a plan; call again with that plan to create exactly those items in one go. Name the project when the user is in more than one.")]
    public async Task<CallToolResult> CaptureWork(
        [Description("The items to create, 1 to 25. Left out when you pass a plan.")] IReadOnlyList<CaptureItem>? items = null,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The plan a preview returned. Creates exactly what it holds and ignores items.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);

        if (plan is not null)
        {
            var sealedPlan = envelope.Open<CapturePlan>(PlanKind, plan, resolved.Id);
            if (await recheck.StaleReasonAsync(resolved.Id, sealedPlan.Items, cancellationToken) is { } stale)
            {
                throw ApiException.Conflict($"{stale} since the preview, so nothing was created. Call capture_work again with the items to preview again.");
            }
            return await CreateAsync(resolved, sealedPlan.Items, cancellationToken);
        }

        if (items is null || items.Count == 0)
        {
            throw ApiException.Validation("Pass at least one item to capture.", "items");
        }
        if (items.Count > CaptureSupport.MaxItems)
        {
            throw ApiException.Validation($"Capture at most {CaptureSupport.MaxItems} items at a time; you passed {items.Count}.", "items");
        }

        var planned = new List<PlannedItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            planned.Add(await support.PlanAsync(items[i], resolved, i, cancellationToken));
        }

        if (planned.Count == 1)
        {
            return await CreateAsync(resolved, planned, cancellationToken);
        }

        var url = await support.BaseUrlAsync(cancellationToken);
        var preview = new CapturePreview(
            resolved.Key, McpOpenUrls.Backlog(url, resolved.Key, "stories"), false,
            [.. planned.Select(p => new PreviewItem(CaptureSupport.TypeName(p.Type), p.Title, p.ParentKey, p.SprintName, p.Points))],
            envelope.Seal(PlanKind, resolved.Id, new CapturePlan(planned)), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return CaptureSupport.Reply(preview, $"Preview of {planned.Count} items for {resolved.Key}; nothing is created yet. Call capture_work again with the `plan` value from the JSON below to create them.");
    }

    [McpServerTool(Name = "break_down"), McpScope(McpScopes.Write)]
    [Description("Split a story or bug into child tasks, or a feature into stories (or an epic into features), from a list of titles with optional points, description (Markdown) and tags. Up to 25 children, created together. They take the parent's sprint; the parent's state is left alone.")]
    public async Task<CallToolResult> BreakDown(
        [Description("The parent's key, such as SHIP-12.")] string parent,
        [Description("The children, 1 to 25.")] IReadOnlyList<BreakDownChild> children,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(parent, ProjectRole.Member, field: "parent", cancellationToken: cancellationToken);
        if (children is null || children.Count == 0 || children.Count > CaptureSupport.MaxItems)
        {
            throw ApiException.Validation($"Pass 1 to {CaptureSupport.MaxItems} children.", "children");
        }
        if (children.Any(c => string.IsNullOrWhiteSpace(c.Title)))
        {
            throw ApiException.Validation("Every child needs a title.", "children");
        }
        if (children.Any(c => c.Description?.Length > MarkdownRenderer.MaxLength))
        {
            throw ApiException.Validation($"A child's description is too long; the limit is {MarkdownRenderer.MaxLength:N0} characters.", "children");
        }

        if (children.Any(c => c.Tags?.Any(string.IsNullOrWhiteSpace) == true))
        {
            throw ApiException.Validation("A child's tags can't be null or blank.", "children");
        }

        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var childType = snapshot.Type switch
        {
            WorkItemTypeId.Epic => WorkItemTypeId.Feature,
            WorkItemTypeId.Feature => WorkItemTypeId.Story,
            WorkItemTypeId.Story or WorkItemTypeId.Bug => WorkItemTypeId.Task,
            _ => throw ApiException.Validation($"{resolved.Key} is a {CaptureSupport.TypeName(snapshot.Type)}; only an epic, feature, story or bug can be broken down.", "parent"),
        };

        var planned = children
            .Select(c => new PlannedItem(childType, c.Title!.Trim(), c.Description, [], null, null, c.Points, [.. c.Tags ?? []], null, resolved.Id, resolved.Key, snapshot.SprintId, null))
            .ToList();
        return await CreateAsync(await support.ProjectOfAsync(resolved, cancellationToken), planned, cancellationToken);
    }

    private async Task<CallToolResult> CreateAsync(ResolvedProject project, IReadOnlyList<PlannedItem> planned, CancellationToken cancellationToken)
    {
        IReadOnlyList<WriteOutcome> outcomes;
        if (planned.Count == 1)
        {
            outcomes = [await store.CreateAsync(CaptureSupport.ToCreate(project.Id, planned[0]), WriteOptions.User, cancellationToken)];
        }
        else
        {
            outcomes = await store.CreateManyAsync([.. planned.Select(p => CaptureSupport.ToCreate(project.Id, p))], WriteOptions.User, cancellationToken);
        }

        var result = await support.CreatedAsync(project, planned, outcomes, cancellationToken);
        return CaptureSupport.Reply(result, $"Created {string.Join(", ", result.Items.Select(i => i.Key))} in {project.Key}.");
    }
}

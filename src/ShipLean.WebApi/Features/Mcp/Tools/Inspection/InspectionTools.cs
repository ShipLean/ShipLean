using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Inspection.AgentSetup;
using ShipLean.Application.Features.Inspection.Views;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Inspection;

// The agent runs of a work item, read through the same views as /api/inspection (K6). Not listed, and not_found when called, while the instance has Inspection off.
[McpServerToolType]
internal sealed class InspectionTools(ItemResolver items, CaptureSupport support, InspectionViews views, AgentSetupService setup)
{
    [McpServerTool(Name = "list_runs"), McpScope(McpScopes.Read)]
    [Description("List the agent runs (Claude Code, Codex CLI, Cursor) linked to a work item: when, which agent, what it did, how long, tokens and errors.")]
    public async Task<CallToolResult> ListRunsAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        var section = await views.SectionAsync(ProjectKey(resolved.Key), resolved.Key, cancellationToken);
        return await ReplyAsync(resolved, section, $"{section.Rows.Count} runs on {resolved.Key}.", cancellationToken);
    }

    [McpServerTool(Name = "get_run"), McpScope(McpScopes.Read)]
    [Description("Read one agent run of a work item: its turns and the rows of what happened. Cite run ids in log_retrospective failure logs.")]
    public async Task<CallToolResult> GetRunAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        [Description("The run id from list_runs.")] string run,
        [Description("all or errors.")] string? filter = null,
        [Description("Where the last page stopped.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        if (filter is not (null or "all" or "errors"))
        {
            throw ApiException.Validation("filter must be all or errors.", "filter");
        }
        var page = await views.RunAsync(ProjectKey(resolved.Key), resolved.Key, run, cancellationToken);
        if (filter == "errors")
        {
            // TODO: errors-first ordering and paging by cursor.
            page = page with { Turns = [.. page.Turns.Select(t => t with { Rows = [.. t.Rows.Where(r => r.IsError)] })] };
        }
        return await ReplyAsync(resolved, page, $"Run {run} on {resolved.Key}.", cancellationToken);
    }

    [McpServerTool(Name = "agent_setup"), McpScope(McpScopes.Read)]
    [Description("The files that make one agent (claude-code, codex-cli or cursor) send telemetry tagged with a work item.")]
    public async Task<CallToolResult> AgentSetupAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        [Description("claude-code, codex-cli or cursor.")] string agent,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        var result = await setup.GetAsync(ProjectKey(resolved.Key), agent, resolved.Key, cancellationToken);
        return await ReplyAsync(resolved, result, $"Setup for {result.AgentLabel} on {resolved.Key}.", cancellationToken);
    }

    private static string ProjectKey(string itemKey) => itemKey[..itemKey.IndexOf('-', StringComparison.Ordinal)];

    private async Task<CallToolResult> ReplyAsync(ResolvedItem resolved, object data, string summary, CancellationToken cancellationToken)
    {
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var result = new InspectionToolResult(project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), data);
        return PlanningReply.Of(result, summary);
    }
}

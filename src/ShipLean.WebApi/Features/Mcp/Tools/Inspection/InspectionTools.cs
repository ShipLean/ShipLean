using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private static readonly JsonSerializerOptions DataOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [McpServerTool(Name = "list_runs"), McpScope(McpScopes.Read)]
    [Description("List the agent runs (Claude Code, Codex CLI, Cursor) linked to a work item: when, which agent, what it did, how long, tokens and errors.")]
    public async Task<CallToolResult> ListRunsAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var section = await views.SectionAsync(project.Key, resolved.Key, cancellationToken);
        return await ReplyAsync(resolved, project, section, $"{section.Rows.Count} runs on {resolved.Key}.", cancellationToken);
    }

    [McpServerTool(Name = "get_run"), McpScope(McpScopes.Read)]
    [Description("Read one agent run of a work item as rows, errors first and then the rest in run order, a page at a time: pass next_cursor back as cursor for the next page. Quote the run id (and the row names) in log_retrospective failure logs so the next agent can find the run.")]
    public async Task<CallToolResult> GetRunAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        [Description("The run id from list_runs.")] string run,
        [Description("all or errors (error rows and the rows they sit under).")] string? filter = null,
        [Description("next_cursor from the previous page; leave out for the first.")] string? cursor = null,
        [Description("Rows per page, 1 to 200; 50 when left out.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        if (filter is not (null or "all" or "errors"))
        {
            throw ApiException.Validation("filter must be all or errors.", "filter");
        }
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var page = await views.RunRowsAsync(project.Key, resolved.Key, run, filter == "errors", cursor, limit ?? RunRowsCursor.DefaultLimit, cancellationToken);
        return await ReplyAsync(resolved, project, page, $"Run {run} on {resolved.Key}: {page.Rows.Count} of {page.TotalRows} rows, {page.ErrorRows} with errors.", cancellationToken);
    }

    [McpServerTool(Name = "agent_setup"), McpScope(McpScopes.Read)]
    [Description("The files that make one agent (claude-code, codex-cli or cursor) send telemetry tagged with a work item.")]
    public async Task<CallToolResult> AgentSetupAsync(
        [Description("The work item's key, such as SHIP-12.")] string item,
        [Description("claude-code, codex-cli or cursor.")] string agent,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, field: "item", cancellationToken: cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var result = await setup.GetAsync(project.Key, agent, resolved.Key, cancellationToken);
        return await ReplyAsync(resolved, project, result, $"Setup for {result.AgentLabel} on {resolved.Key}.", cancellationToken);
    }

    private async Task<CallToolResult> ReplyAsync(ResolvedItem resolved, ResolvedProject project, object data, string summary, CancellationToken cancellationToken)
    {
        // The view goes out as the K5 routes send it (camelCase, enums as camelCase words), not in the default serializer's PascalCase.
        var result = new InspectionToolResult(project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), JsonSerializer.SerializeToElement(data, data.GetType(), DataOptions));
        return PlanningReply.Of(result, summary);
    }
}

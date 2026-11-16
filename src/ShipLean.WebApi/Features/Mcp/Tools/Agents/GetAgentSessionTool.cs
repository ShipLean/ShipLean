using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Orient;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

[McpServerToolType]
internal sealed class GetAgentSessionTool(ItemResolver items, AgentSessionMcpReads reads, IAgentSessionDetails details, IAgentAddresses addresses)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    [McpServerTool(Name = "get_agent_session"), McpScope(McpScopes.Read)]
    [Description("One agent session of a work item: how it ended (end_reason, end_text and, for those who may see it, end_detail), its activities newest first, its delivery attempts and its run log. Pass exactly one of session (the id from get_work_item's agent_sessions) or item (a key, like SHIP-123: its newest session).")]
    public async Task<CallToolResult> GetAgentSessionAsync(
        [Description("The session id.")] string? session = null,
        [Description("A work item key, like SHIP-123: reads that item's newest session.")] string? item = null,
        [Description("The next_before of a previous call, to read older activities.")] string? before = null,
        [Description("How many activities to return: default 50, at most 100.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(session) == string.IsNullOrWhiteSpace(item))
        {
            throw ApiException.Validation("Pass exactly one of session or item.", string.IsNullOrWhiteSpace(session) ? "item" : "session");
        }

        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaxLimit)
        {
            throw ApiException.Validation($"limit must be between 1 and {MaxLimit}.", "limit");
        }

        Guid? sessionId = null;
        var itemKey = item;
        if (!string.IsNullOrWhiteSpace(session))
        {
            if (!Guid.TryParse(session, out var parsed))
            {
                throw ApiException.Validation("session must be a session id, a uuid.", "session");
            }

            sessionId = parsed;
            itemKey = await reads.ItemKeyOfAsync(parsed, cancellationToken) ?? throw ApiException.NotFound("No agent session with that id was found.");
        }

        var resolved = await items.ResolveAsync(itemKey!, ProjectRole.Member, field: sessionId is null ? "item" : "session", cancellationToken: cancellationToken);
        var read = await details.ReadAsync(resolved.Id, resolved.Key, sessionId, before, take, cancellationToken);

        var row = read.Session;
        var end = AgentSessionVisibility.Apply(row.EndReason, row.EndDetail, read.RunLog, resolved.Role);
        var projectKey = PlanningReply.ProjectKeyOf(resolved.Key);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var result = new GetAgentSessionResult(
            projectKey,
            McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key),
            new GetAgentSessionResult.SessionView(
                row.Id, read.ItemKey, row.Connection, row.State, row.Started.ToString("O"),
                row.State == "stale" ? null : row.Ended?.ToString("O"),
                end.EndReason, end.EndText, end.EndDetail,
                end.RunLog is { } log ? new GetAgentSessionResult.RunLogView(log.Name, log.SizeBytes) : null),
            [.. read.Activities.Select(a => new GetAgentSessionResult.ActivityView(a.Id, a.Type, a.Body, a.Action, a.Parameter, a.Result, a.Truncated, a.At.ToString("O")))],
            read.NextBefore,
            end.ShowDeliveries
                ? [.. read.Deliveries.Select(d => new GetAgentSessionResult.DeliveryView(d.Action, d.Attempt, d.At.ToString("O"), d.HttpStatus, d.Error, d.DurationMs, d.NextAttemptAt?.ToString("O")))]
                : []);
        return OrientReply.Of(result, $"{read.ItemKey} agent session {row.Id}: {row.State}{(end.EndText is null ? string.Empty : $". {end.EndText}")}");
    }
}

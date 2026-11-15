using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Reviews;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Reviews;

// log_review: the review agent's record of one review of a task (D15). The Feature timeline draws a marker per review.
[McpServerToolType]
internal sealed class ReviewTools(ItemResolver items, IReviewLog reviews, IAgentAddresses addresses)
{
    private const string Approved = "approved";
    private const string ChangesRequested = "changes_requested";

    [McpServerTool(Name = "log_review"), McpScope(McpScopes.Write)]
    [Description("Log one review of a task: the verdict, who and when. Call it once per round, after the Review · comment: 'changes_requested' when there is any blocking finding, else 'approved'. It only appends; the task's state does not change. Tasks only. The Feature timeline shows each review after the task's bar.")]
    public async Task<CallToolResult> LogReview(
        [Description("The key of the task, such as SHIP-12.")] string item,
        [Description("'approved' or 'changes_requested'.")] string verdict,
        [Description("A short note, optional.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        var parsed = (verdict ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Approved => ReviewVerdict.Approved,
            ChangesRequested => ReviewVerdict.ChangesRequested,
            _ => throw ApiException.Validation($"verdict must be \"{Approved}\" or \"{ChangesRequested}\".", "verdict"),
        };
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var logged = await reviews.AppendAsync(new ReviewInput(resolved.Id, parsed, note), cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var projectKey = resolved.Key[..resolved.Key.IndexOf('-')];
        var word = parsed == ReviewVerdict.Approved ? Approved : ChangesRequested;

        var result = new LogReviewResult(
            projectKey, McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key), resolved.Key, word, logged.At.ToString("O", CultureInfo.InvariantCulture), logged.Round);
        return McpToolResult.Of(JsonSerializer.SerializeToElement(result), $"Review logged on {resolved.Key}: {word} (round {logged.Round})");
    }
}

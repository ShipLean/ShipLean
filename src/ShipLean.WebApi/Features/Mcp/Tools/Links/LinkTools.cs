using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;

namespace ShipLean.WebApi.Features.Mcp.Tools.Links;

// What an agent can say about a link ShipLean can't read itself. The `link` tool that adds one stays with the other discussion tools.
[McpServerToolType]
internal sealed class LinkTools(ILinkStateReports reports, CaptureSupport support)
{
    [McpServerTool(Name = "report_link_state"), McpScope(McpScopes.Write)]
    [Description("Report what you can see at an address ShipLean can't read (a private GitLab merge request, say): its title and its state (open, draft, merged or closed) as of a time. It applies to every link with that address on the work items you can reach, and the card shows it as reported by you.")]
    public async Task<CallToolResult> ReportLinkState(
        [Description("The address, exactly as it is linked.")] string url,
        [Description("When what you report was true, as an ISO 8601 time, such as 2026-10-25T09:00:00Z.")] string as_of,
        [Description("The page's title.")] string? title = null,
        [Description("open, draft, merged or closed.")] string? state = null,
        CancellationToken cancellationToken = default)
    {
        if (!DateTimeOffset.TryParse(as_of, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var asOf))
        {
            throw ApiException.Validation("as_of must be an ISO 8601 time, such as 2026-10-25T09:00:00Z.", "as_of");
        }

        LinkState? parsed = null;
        if (!string.IsNullOrWhiteSpace(state))
        {
            parsed = Enum.TryParse<LinkState>(state.Trim(), ignoreCase: true, out var value) && Enum.IsDefined(value)
                ? value
                : throw ApiException.Validation($"\"{state}\" is not a state: use open, draft, merged or closed.", "state");
        }

        await reports.ReportAsync(url ?? string.Empty, title, parsed, asOf.ToUniversalTime(), cancellationToken);
        var result = new ReportLinkStateResult(null, await support.BaseUrlAsync(cancellationToken), url!.Trim(), parsed?.ToString().ToLowerInvariant(), asOf.ToUniversalTime());
        return CaptureSupport.Reply(result, $"Reported {url.Trim()}{(parsed is null ? "" : $" as {result.State}")}.");
    }
}

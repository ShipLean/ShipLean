using ShipLean.Application.Features.LinearApi;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.LinearApi;

// A session as Linear's AgentSession. status is worked out from ShipLean's state (LinearStatuses). externalUrls is the stored
// {url, label} list, externalLinks the same as Linear's {label, url} objects; plan is the stored {content, status} list.
internal static class LinearSessionMap
{
    public static IReadOnlyDictionary<string, object?> Of(LinearSession session, string baseUrl, LinearRequest request)
    {
        var urls = LinearJson.Plain(session.ExternalUrls) as List<object?> ?? [];
        return new Dictionary<string, object?>
        {
            ["id"] = session.Id.ToString(),
            ["slugId"] = session.Id.ToString("N")[..8],
            ["status"] = LinearStatuses.Of(session.State),
            ["type"] = "commentThread",
            ["url"] = McpOpenUrls.WorkItem(baseUrl, session.ProjectKey, session.ItemKey),
            ["createdAt"] = session.CreatedAt,
            ["startedAt"] = session.CreatedAt,
            ["updatedAt"] = session.LastActivityAt,
            ["endedAt"] = session.EndedAt,
            ["context"] = new Dictionary<string, object?>(),
            ["externalUrls"] = urls,
            ["externalLinks"] = urls.OfType<Dictionary<string, object?>>()
                .Select(u => new Dictionary<string, object?> { ["label"] = u.GetValueOrDefault("label") ?? string.Empty, ["url"] = u.GetValueOrDefault("url") ?? string.Empty })
                .ToList(),
            ["plan"] = LinearJson.Plain(session.Plan),
            ["creator"] = LinearPersonMap.Of(session.Creator),
            ["appUser"] = LinearPersonMap.App(request.Caller),
            ["issue"] = (LinearLazy)(async context =>
            {
                var issue = await request.Reads.FindIssueAsync(request.Caller.ConnectionId, session.WorkItemId.ToString(), context.RequestAborted);
                object? mapped = issue is null ? null : LinearIssueMap.Of(issue, baseUrl, request);
                return mapped;
            }),
            ["activities"] = (LinearLazy)(async context =>
            {
                var page = await request.Reads.ActivitiesAsync(request.Caller.ConnectionId, session.Id, LinearPaging.First(context), LinearPaging.After(context), context.RequestAborted);
                return LinearPaging.Connection([.. page.Items.Select(a => LinearActivityMap.Of(a, request.Caller))], page.HasNextPage);
            }),
        };
    }
}

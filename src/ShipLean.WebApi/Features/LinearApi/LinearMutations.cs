using HotChocolate.Resolvers;
using ShipLean.Application.Features.LinearApi;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.LinearApi;

// The Mutation fields (SHIP-187). Each one works through IAgentReports as the calling connection, so a session or item that
// is not that connection's is NOT_FOUND. The payloads are Linear's: success, lastSyncId (ShipLean has no sync log, so 0) and the
// entity. A rule the report breaks (a prompt activity, an ephemeral response) is an INVALID_INPUT error.
internal static class LinearMutations
{
    private static AgentReporter Reporter(LinearRequest request) => new(request.Caller.ConnectionId, request.Caller.Name);

    public static async ValueTask<object?> AgentActivityCreate(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var create = LinearInput.Activity(LinearInput.Of(context));
        var id = await LinearInput.Guarded(async () => await context.Service<IAgentReports>().CreateActivityAsync(Reporter(request), create, context.RequestAborted));
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["lastSyncId"] = 0.0,
            ["agentActivity"] = new Dictionary<string, object?> { ["id"] = id.ToString() },
        };
    }

    public static ValueTask<object?> AgentSessionUpdate(IResolverContext context) => UpdateSession(context);

    public static ValueTask<object?> AgentSessionUpdateExternalUrl(IResolverContext context) => UpdateSession(context);

    // Both mutations take the same url fields; agentSessionUpdate adds plan, which the other input does not have.
    private static async ValueTask<object?> UpdateSession(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var sessionId = LinearInput.SessionId(context.ArgumentValue<string>("id"));
        var change = LinearInput.Change(LinearInput.Of(context));
        await LinearInput.Guarded(async () =>
        {
            await context.Service<IAgentReports>().UpdateSessionAsync(Reporter(request), sessionId, change, context.RequestAborted);
            return true;
        });
        var session = await request.Reads.FindSessionAsync(request.Caller.ConnectionId, sessionId, context.RequestAborted) ?? throw LinearErrors.NotFound("AgentSession");
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["lastSyncId"] = 0.0,
            ["agentSession"] = LinearSessionMap.Of(session, await request.BaseUrlAsync(context.RequestAborted), request),
        };
    }

    public static async ValueTask<object?> CommentCreate(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var input = LinearInput.Of(context);
        var issue = LinearInput.Text(input, "issueId") ?? throw LinearErrors.Invalid("issueId is required.");
        var comment = await LinearInput.Guarded(async () =>
            await context.Service<IAgentReports>().CreateCommentAsync(Reporter(request), issue, LinearInput.Text(input, "body") ?? string.Empty, context.RequestAborted));
        var item = await request.Reads.FindIssueAsync(request.Caller.ConnectionId, comment.WorkItemId.ToString(), context.RequestAborted) ?? throw LinearErrors.NotFound("Issue");
        var issueUrl = McpOpenUrls.WorkItem(await request.BaseUrlAsync(context.RequestAborted), item.ProjectKey, item.Key);
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["lastSyncId"] = 0.0,
            ["comment"] = LinearCommentMap.Of(new LinearComment(comment.Id, comment.WorkItemId, comment.Body, comment.CreatedAt, comment.CreatedAt, null), issueUrl),
        };
    }
}

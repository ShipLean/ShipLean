namespace ShipLean.Contracts.Agents;

// The comment endpoints' hook into agent dispatch. Called after a comment a person wrote through the app (cookie or proxy session)
// is committed; it queues a `prompted` webhook when the item has a live agent session. It never throws into the caller: a failure
// is logged. The comment store never calls it, so MCP tools, imports, webhooks and agent writes cannot prompt.
public interface IAgentPrompts
{
    Task CommentAddedAsync(Guid commentId, CancellationToken cancellationToken = default);
}

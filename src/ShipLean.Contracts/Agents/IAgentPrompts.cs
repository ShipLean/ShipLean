namespace ShipLean.Contracts.Agents;

// The comment store's hook into agent dispatch. Called after a comment by a person is committed; it queues a `prompted`
// webhook when the item has a live agent session. It never throws into the caller: a failure is logged.
// A comment that carries an agent stamp is never passed here (see ICommentStore.AddAsAgentAsync).
public interface IAgentPrompts
{
    Task CommentAddedAsync(Guid commentId, CancellationToken cancellationToken = default);
}

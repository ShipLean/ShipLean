using ShipLean.Contracts.Errors;

namespace ShipLean.Contracts.Agents;

// What an agent writes back through the Linear-compatible endpoint. Every call is scoped to the reporter's connection: a session
// or item that is another connection's, or does not exist, is ApiException.NotFound, so one answer covers both. A bad shape
// (a prompt activity, an ephemeral response, a malformed url) is ApiException.Validation.
public interface IAgentReports
{
    // Stores the activity, replaces the session's ephemeral one, moves the session state and, for a response or error, posts the
    // body as a comment stamped with the connection name. Returns the activity id.
    Task<Guid> CreateActivityAsync(AgentReporter reporter, AgentActivityCreate create, CancellationToken cancellationToken = default);

    // Applies the change, and links every GitHub or GitLab pull or merge request url it adds to the session's item.
    Task UpdateSessionAsync(AgentReporter reporter, Guid sessionId, AgentSessionChange change, CancellationToken cancellationToken = default);

    // A comment on an item the connection has a session on, written as the user who started the connection's newest session on it.
    // `issue` is the item's id or key.
    Task<AgentComment> CreateCommentAsync(AgentReporter reporter, string issue, string body, CancellationToken cancellationToken = default);
}

using System.Data.Common;

namespace ShipLean.Contracts.Agents;

// Agent dispatch's session starter, for features that start an agent session of their own (a sprint demo).
public interface IAgentSessionStarter
{
    // The agent check at project level: null when an agent can start for the project, else why not. connectionId, when given,
    // wins over the project's connection, so an override works on a project with none.
    Task<AgentStartBlock?> PlanProjectAsync(Guid projectId, Guid? connectionId, CancellationToken cancellationToken);

    // What a session on the work item would start with, or why it cannot (a live session on the item included).
    Task<AgentStartPlan> PlanAsync(Guid workItemId, Guid? connectionId, string? prompt, CancellationToken cancellationToken);

    // Opens a session whose prompt is made only by the starter that wins the race, on the winning transaction, so a prompt
    // that issues or revokes a token never does it for a loser. Anything thrown after the session insert rolls back the
    // session, its delivery and whatever winnerPrompt wrote. mode is stored on the session.
    // A propose session (D9, D10, D17) needs the item's github_issue (ApiException.Validation otherwise). Its context is allowlisted:
    // the issue's repository, number, title and body (from github_issue), the author's login, the comments mirrored through
    // IMirroredComments, and relatedItems (at most MaxRelatedItems; ignored in implement mode). Its `created` payload says
    // shipleanMode: propose, and leaves the issue url, labels and creator name empty (creator email stays, for the relay's allow-list).
    // fence (optional) runs on the winning transaction after the session insert and before winnerPrompt, where the new session is
    // the item's only live one; it returns false to abort the start: everything rolls back and the outcome has neither SessionId
    // nor Block. A caller claims an idempotency key there (the round job worker's round-session:<jobKey>).
    Task<AgentStartOutcome> StartWinnerAsync(
        Guid workItemId, Guid creatorId, string? fallbackBaseUrl, Guid? connectionId, Func<DbTransaction, CancellationToken, Task<string>> winnerPrompt, CancellationToken cancellationToken,
        AgentSessionMode mode = AgentSessionMode.Implement, IReadOnlyList<AgentRelatedWork>? relatedItems = null, Func<DbTransaction, CancellationToken, Task<bool>>? fence = null);

    // The most related items a propose session takes (the rounds' "Closest existing items").
    public const int MaxRelatedItems = 8;
}

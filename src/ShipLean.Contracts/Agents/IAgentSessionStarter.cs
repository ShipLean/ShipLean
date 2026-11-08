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
    // session, its delivery and whatever winnerPrompt wrote. mode is stored on the session; a propose session's context carries only
    // the comments mirrored from GitHub, and its `created` payload says shipleanMode: propose.
    Task<AgentStartOutcome> StartWinnerAsync(
        Guid workItemId, Guid creatorId, string? fallbackBaseUrl, Guid? connectionId, Func<DbTransaction, CancellationToken, Task<string>> winnerPrompt, CancellationToken cancellationToken,
        AgentSessionMode mode = AgentSessionMode.Implement);
}

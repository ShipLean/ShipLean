using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Runs;

// The one place that decides who gets a sprint demo's upload link. The button and the automatic run (C1, C3) start a session through
// StartSessionAsync; the MCP tool (C1) hands the prompt over through HandOverAsync. A token, a revoke or a demo_requested_* write
// happens only for the caller that wins, in the transaction that wins it.
public interface IDemoRunStarter
{
    // The sprint's BAU demo item, made on first use (one per sprint, however many callers race).
    Task<Guid> GetOrCreateItemAsync(Guid projectId, Guid sprintId, string sprintName, CancellationToken cancellationToken);

    // Button and automatic: starts an agent session whose prompt carries the upload link. LiveSession when another caller won or a session is live.
    Task<AgentStartOutcome> StartSessionAsync(DemoRunRequest request, CancellationToken cancellationToken);

    // MCP: no session; the prompt and the link go back to the caller. 409 while an agent session is live on the item.
    Task<SprintDemoHandover> HandOverAsync(DemoRunRequest request, CancellationToken cancellationToken);
}

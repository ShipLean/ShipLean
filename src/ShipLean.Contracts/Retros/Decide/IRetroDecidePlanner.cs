using ShipLean.Contracts.Retros;

namespace ShipLean.Contracts.Retros.Decide;

// Decide what to do with a scope's retro points. Preview writes nothing; apply does exactly the plan it is given, after re-checking what is decided.
public interface IRetroDecidePlanner
{
    Task<DecideOutcome> PreviewAsync(Guid projectId, DecidePlan request, CancellationToken cancellationToken);

    Task<DecideOutcome> ApplyAsync(Guid projectId, DecidePlan plan, RetroActor actor, CancellationToken cancellationToken);

    // The same apply, also saying what became of each action of the plan, in plan order.
    Task<DecideApplyResult> ApplyWithResultsAsync(Guid projectId, DecidePlan plan, RetroActor actor, CancellationToken cancellationToken);
}

using System.Data.Common;

namespace ShipLean.Contracts.Sprints;

// Recomputes the stored capacity points of a project's sprints that have not ended and have a stored velocity, from the project's
// working days and holiday country, the sprint's dates and each member's days. A typed Days above the new working-day count is
// clamped to it. Sprints with no velocity (old points-only capacity) and ended sprints are never touched. Callers: the sprint
// update, the project settings save and the holiday refresh. Returns the ids of the sprints whose stored points or days changed,
// so the caller can publish SprintChanged for them (this does not). With a transaction it joins it and does not commit.
public interface ISprintCapacityRecompute
{
    Task<IReadOnlyList<Guid>> RecomputeAsync(Guid projectId, DbTransaction? transaction, CancellationToken cancellationToken = default);
}

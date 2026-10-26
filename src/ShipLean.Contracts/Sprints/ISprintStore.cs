using System.Data.Common;

namespace ShipLean.Contracts.Sprints;

// Sprints and their capacity. Callers check access first. Nothing is deleted.
public interface ISprintStore
{
    // Joins the caller's transaction when given. Throws ApiException.NotFound (unknown project), ApiException.Validation (blank name, end before start, overlaps another sprint), ApiException.Conflict (name taken).
    Task<SprintDto> CreateAsync(Guid projectId, SprintCreate create, DbTransaction? transaction, CancellationToken cancellationToken = default);

    // Throws ApiException.NotFound (unknown sprint), ApiException.Validation (blank name, end before start, overlaps another sprint), ApiException.Conflict (name taken).
    Task<SprintDto> UpdateAsync(Guid id, SprintUpdate update, CancellationToken cancellationToken = default);

    // Throws ApiException.NotFound (unknown sprint).
    Task<SprintCapacityDto> GetCapacityAsync(Guid sprintId, CancellationToken cancellationToken = default);

    // Replaces the whole set of entered rows: a member left out has their entry cleared (back to velocity), one listed again gets it back. Throws ApiException.NotFound (unknown sprint), ApiException.Validation (negative points, user not a member).
    Task<SprintCapacityDto> PutCapacityAsync(Guid sprintId, SprintCapacityPut capacities, CancellationToken cancellationToken = default);

    // The ended sprint's unfinished stories and bugs, each with a default target (the next sprint). Throws ApiException.NotFound (unknown sprint).
    Task<RolloverDto> GetRolloverAsync(Guid sprintId, CancellationToken cancellationToken = default);

    // Moves the listed items and marks the sprint's rollover decided, all or nothing. Throws ApiException.NotFound (unknown sprint, item or target), ApiException.Validation (sprint not ended: its end date is after today; a sprint may be rolled over on its end day; item not in the sprint, target in another project).
    Task<SprintDto> RolloverAsync(Guid sprintId, RolloverRequest request, CancellationToken cancellationToken = default);
}

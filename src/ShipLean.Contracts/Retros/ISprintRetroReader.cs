namespace ShipLean.Contracts.Retros;

// The Retro tab of one sprint.
public interface ISprintRetroReader
{
    Task<SprintRetroDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default);
}

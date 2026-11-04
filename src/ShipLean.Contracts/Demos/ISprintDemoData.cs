namespace ShipLean.Contracts.Demos;

public interface ISprintDemoData
{
    Task<SprintDemoData> ReadAsync(Guid sprintId, CancellationToken cancellationToken = default);
}

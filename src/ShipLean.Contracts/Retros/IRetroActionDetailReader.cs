namespace ShipLean.Contracts.Retros;

// One action item as the action's own page shows it (GET api/retros/actions/{workItemId}). A work item with no retro_action row is not_found.
public interface IRetroActionDetailReader
{
    Task<RetroActionDetailDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default);
}

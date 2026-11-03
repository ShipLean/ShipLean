using ShipLean.Contracts.Retros;
using Loader = ShipLean.Application.Features.Retros.People.RetroActionDetailReader;

namespace ShipLean.Application.Features.Retros.Actions;

// GET api/retros/actions/{workItemId}: the action item's ref, theme, the sprint it was raised in and its live points. The load is the one
// the writes reply with, so the page and a write's reply cannot disagree; an item with no retro_action row is not_found.
internal sealed class RetroActionDetailReader(Loader loader) : IRetroActionDetailReader
{
    public Task<RetroActionDetailDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => loader.LoadAsync(workItemId, cancellationToken);
}

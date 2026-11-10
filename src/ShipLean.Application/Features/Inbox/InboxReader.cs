using ShipLean.Contracts.Inbox;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

public sealed class InboxReader(TimeProvider clock) : IInboxReader
{
    // TODO: compose the registered IInboxAskSource rows into ordered items; until then the Inbox is empty.
    public Task<InboxDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new InboxDto(projectId, 0, 0, false, DisplayDate.Clock(clock.GetUtcNow(), clock), [], []));
}

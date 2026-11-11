using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

// The current user's Inbox in one project: every registered IInboxAskSource composed into rows in display order, with the
// items' metadata, mention labels "@1…" per item, WaitedOnBy, OtherProjects, EverWaited and LoadedAtLabel. Checks no access:
// the endpoint checks project membership first.
public interface IInboxReader
{
    Task<InboxDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}

using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

// What the current user answered in one project in the last InboxRules.AnsweredWindow, with what happened next on each item.
// Checks no access: the endpoint checks project membership first.
public interface IAnsweredReader
{
    Task<AnsweredDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}

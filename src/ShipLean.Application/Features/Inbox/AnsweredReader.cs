using ShipLean.Contracts.Inbox;

namespace ShipLean.Application.Features.Inbox;

public sealed class AnsweredReader : IAnsweredReader
{
    // TODO: read the user's answers in the last InboxRules.AnsweredWindow and what happened next; until then there are none.
    public Task<AnsweredDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new AnsweredDto(0, 0, []));
}

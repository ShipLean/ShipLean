using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Inbox;

public sealed class MentionWrites : IMentionWrites
{
    // TODO: find the current user's mention, check membership and mark it seen; until then no mention is found.
    public Task SeenAsync(Guid mentionId, CancellationToken cancellationToken = default)
        => throw ApiException.NotFound("Mention not found.");

    // TODO: find the current user's mention, check membership and post the reply; until then no mention is found.
    public Task<Guid> ReplyAsync(Guid mentionId, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw ApiException.Validation("Write a reply.", "body");
        }

        throw ApiException.NotFound("Mention not found.");
    }
}

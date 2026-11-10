namespace ShipLean.Application.Features.Inbox;

// The current user's answers to a mention. Unlike the other Inbox services it checks access itself: the mention must be the
// current user's and the user a Member of its item's project, else NotFound.
public interface IMentionWrites
{
    // Marks an open mention seen; any other state is left as it is.
    Task SeenAsync(Guid mentionId, CancellationToken cancellationToken = default);

    // Posts the reply as a comment that starts "@<mentioner's full name>, " and marks the mention replied. A blank body is
    // Validation on "body"; a withdrawn mention is Conflict. Returns the new comment's id.
    Task<Guid> ReplyAsync(Guid mentionId, string body, CancellationToken cancellationToken = default);
}

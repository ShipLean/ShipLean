namespace ShipLean.Application.Features.Inbox;

// One kind of open ask waiting on a person (decisions and questions, stale status lines, mentions). The reader composes every
// registered source. A source leaves mention labels as "@"; the reader numbers them per item. Checks no access.
public interface IInboxAskSource
{
    // The open asks waiting on userId in one project, in no particular order.
    Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);

    // The number of open asks waiting on userId in each project that has any.
    Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default);

    // Whether anything of this kind has ever waited on userId, open or not (the first-run empty state).
    Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default);
}

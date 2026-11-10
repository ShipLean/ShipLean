namespace ShipLean.Contracts.Asks;

// A work item's ask lines for its History, newest first. Decisions raised at the same moment by the same person and agent
// fold into one line ("D1–D5"). Trusts its caller: the caller checks access at Member first.
public interface IAskHistory
{
    Task<IReadOnlyList<AskHistoryLineDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);
}

namespace ShipLean.Contracts.Asks;

// Records decisions an agent made. Trusts its caller: the caller checks access at Member first. No number takes the next;
// a free number takes exactly that; an open number is edited (an `edited` event). A settled or withdrawn number throws
// Validation and the whole call writes nothing. A record with Settled is created confirmed or changed, from the description.
// Publishes AsksChanged after commit.
public interface IDecisionRecorder
{
    Task<RecordedDecisions> RecordAsync(Guid workItemId, IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken = default);
}

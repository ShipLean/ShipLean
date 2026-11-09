using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Decisions.Record;

// TODO: the decision recorder over the decision and ask_event tables replaces this stub.
public sealed class DecisionRecorder : IDecisionRecorder
{
    public Task<RecordedDecisions> RecordAsync(Guid workItemId, IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

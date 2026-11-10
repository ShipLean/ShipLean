namespace ShipLean.Contracts.Asks;

// The numbers IDecisionRecorder created and the open ones it edited.
public sealed record RecordedDecisions(IReadOnlyList<int> Created, IReadOnlyList<int> Updated);

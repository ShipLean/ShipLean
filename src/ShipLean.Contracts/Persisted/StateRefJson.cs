namespace ShipLean.Contracts.Persisted;

// A state a transition names: the id it was saved with and the name it had then (kept so an archived state can be re-bound by name).
public sealed record StateRefJson(Guid StateId, string Name);

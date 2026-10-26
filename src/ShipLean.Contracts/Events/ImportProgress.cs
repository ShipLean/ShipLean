using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Events;

public sealed record ImportProgress(Guid ProjectId, Guid JobId, int Percent, ImportJobState State, string Message) : ProjectEvent(ProjectId);

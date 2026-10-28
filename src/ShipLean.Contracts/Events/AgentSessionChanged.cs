namespace ShipLean.Contracts.Events;

// An agent session on a work item, or one of its activities, changed (opened, state moved, activity written, prompt queued):
// an open work item page refetches its agent panel.
public sealed record AgentSessionChanged(Guid ProjectId, Guid WorkItemId, Guid SessionId) : ProjectEvent(ProjectId);

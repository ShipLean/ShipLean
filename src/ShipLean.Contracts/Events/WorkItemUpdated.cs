namespace ShipLean.Contracts.Events;

// Fields are the ids of the fields that changed. Pulled: a work item rule amended the change with a "pulled" notice (an unassigned card taken by the person who moved it).
public sealed record WorkItemUpdated(Guid ProjectId, Guid Id, int Revision, IReadOnlyList<string> Fields, bool Pulled) : ProjectEvent(ProjectId);

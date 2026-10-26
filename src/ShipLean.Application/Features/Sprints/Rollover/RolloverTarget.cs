namespace ShipLean.Application.Features.Sprints;

// What a rollover needs of a work item it moves: where it is and the revision the patch expects.
public sealed record RolloverTarget(Guid Id, string Key, Guid? SprintId, int Revision);

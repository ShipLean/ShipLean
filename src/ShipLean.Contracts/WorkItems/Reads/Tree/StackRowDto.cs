using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems.Reads.Tree;

// One row of the backlog stack. HasAcceptanceCriteria: the item has at least one acceptance criterion. Revision is the
// item's current revision (what an edit sends back to be checked for a conflict). Tags are the item's tags, in stored order.
public sealed record StackRowDto(
    Guid Id,
    string Key,
    string Title,
    TypeRef Type,
    StateRef State,
    decimal? Points,
    ParentRefDto? Parent,
    SprintRef? Sprint,
    UserRef? Assignee,
    bool HasAcceptanceCriteria,
    DateTimeOffset ChangedAt,
    IReadOnlyList<string> Tags,
    int Revision);

namespace ShipLean.Application.Features.LinearApi;

// A work item as the Linear-compatible API shows it (Linear's Issue). Description already carries the acceptance criteria as a
// checklist, as the webhook does. Number is the part of the key after the dash.
public sealed record LinearIssue(
    Guid Id,
    string Key,
    int Number,
    string Title,
    string? Description,
    string[] Tags,
    Guid ProjectId,
    string ProjectKey,
    Guid StateId,
    int Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    LinearPerson? Creator,
    LinearPerson? Assignee,
    Guid? ParentId,
    Guid? FeatureId);

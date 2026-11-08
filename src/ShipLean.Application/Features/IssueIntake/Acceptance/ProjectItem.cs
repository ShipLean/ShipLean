using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// An item of the accepting project that a proposal names (its parent, or the item it duplicates).
public sealed record ProjectItem(Guid Id, string Key, WorkItemTypeId Type, string Title);

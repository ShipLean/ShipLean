using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Import;

// ProjectId is null until the run has created a new target project. StartedAt: when Start or Re-run was pressed.
public sealed record ImportJobDto(Guid Id, string JiraProjectKey, string ProjectKey, Guid? ProjectId, ImportJobState Status, ImportJobProgressJson Progress, DateTimeOffset? StartedAt, string? StartedLabel, DateTimeOffset? FinishedAt, string? FinishedLabel, UserRef? CreatedBy);

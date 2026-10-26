namespace ShipLean.Application.Features.Import.Jobs;

// An import_job row with its creator, as Dapper reads it. Status and Progress are the stored text.
public sealed record ImportJobRow(
    Guid Id, Guid ConnectionId, string JiraProjectKey, string ProjectKey, Guid? ProjectId, string Status, string Progress,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, Guid? CreatorId, string? CreatorName, string? CreatorInitials, string? CreatorColour, string? CreatorPhotoUrl);

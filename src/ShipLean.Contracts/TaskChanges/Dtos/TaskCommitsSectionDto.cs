namespace ShipLean.Contracts.TaskChanges.Dtos;

// GET api/work-items/{id}/commits, and the answer to adding a commit. HeadCommitId is the id of the newest live commit, null when there are none.
public sealed record TaskCommitsSectionDto(
    Guid WorkItemId,
    IReadOnlyList<TaskCommitRowDto> Commits,
    TaskCommitTotalsDto Totals,
    IReadOnlyList<TaskChangeFilePathDto> FilePaths,
    int OpenComments,
    string? HeadCommitId);

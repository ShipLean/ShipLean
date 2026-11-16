namespace ShipLean.Contracts.TaskChanges.Dtos;

// GET api/work-items/{id}/changes?commit=all|<commitId>. ItemType is "task" or "bug". Selected is "all" or a work_item_commit id.
// Mode is "combined" (the commits follow one another and show as one diff), "perCommit" or "single".
public sealed record TaskChangesDto(
    Guid WorkItemId,
    string ItemKey,
    string ItemTitle,
    string ItemType,
    string StateCategory,
    IReadOnlyList<TaskCommitRowDto> Commits,
    string Selected,
    string Mode,
    string? HeadCommitId,
    IReadOnlyList<DiffPartDto> Parts,
    IReadOnlyList<CodeCommentDto> Comments,
    int OpenComments);

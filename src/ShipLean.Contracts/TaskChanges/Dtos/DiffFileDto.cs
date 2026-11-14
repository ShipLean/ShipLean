namespace ShipLean.Contracts.TaskChanges.Dtos;

// One file of a diff. Status is "added", "modified", "deleted" or "renamed". Patch is the git unified diff of the file. PatchId is the
// lower-case hex sha-1 of Patch, so a file whose diff did not change keeps its Viewed mark.
public sealed record DiffFileDto(
    string Path,
    string? OldPath,
    string Status,
    int Additions,
    int Deletions,
    string Patch,
    string PatchId,
    int OpenComments);

namespace ShipLean.Contracts.TaskChanges.Dtos;

// One diff the page shows. Sha is set for a single commit, BaseSha and HeadSha for a combined range; HeadSha is always set.
// All three are git shas inside Repo.
public sealed record DiffPartDto(string Repo, string? Sha, string? BaseSha, string HeadSha, IReadOnlyList<DiffFileDto> Files);

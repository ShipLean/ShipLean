namespace ShipLean.Contracts.TaskChanges;

public sealed record TaskCommit(
    Guid Id,
    RepoRef Repo,
    string Sha,
    int Position,
    string Subject,
    string AuthorName,
    DateTimeOffset AuthoredAt,
    int Additions,
    int Deletions,
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> ParentShas,
    CommitSource AddedVia,
    string AddedBy,
    bool AddedByAgent,
    DateTimeOffset AddedAt);

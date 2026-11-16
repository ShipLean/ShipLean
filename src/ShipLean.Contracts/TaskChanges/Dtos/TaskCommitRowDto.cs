namespace ShipLean.Contracts.TaskChanges.Dtos;

// One commit of a Task or Bug as the Changes section and page list it. Id is the work_item_commit id: everywhere a single commit is
// selected or compared it is this id, never a bare sha, because the same sha can sit in two repositories. Provider is "github" or
// "gitlab"; Access is "ok", "unreadable" or "missing". Time comes only as the server's label.
public sealed record TaskCommitRowDto(
    Guid Id,
    string Repo,
    string RepoUrl,
    string Provider,
    string Sha,
    string ShortSha,
    string Subject,
    string AuthorName,
    string AuthoredAtLabel,
    string AddedBy,
    bool AddedByAgent,
    int Additions,
    int Deletions,
    int FilesChanged,
    string CommitUrl,
    string Access);

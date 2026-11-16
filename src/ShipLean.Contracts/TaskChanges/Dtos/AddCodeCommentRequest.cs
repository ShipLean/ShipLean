namespace ShipLean.Contracts.TaskChanges.Dtos;

// POST api/work-items/{id}/code-comments. Side is "old" or "new".
public sealed record AddCodeCommentRequest(string Repo, string Sha, string Path, string Side, int Line, int? StartLine, string LineText, string Body);

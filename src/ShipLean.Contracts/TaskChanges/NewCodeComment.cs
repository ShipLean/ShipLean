namespace ShipLean.Contracts.TaskChanges;

public sealed record NewCodeComment(string Repo, string Sha, string Path, CodeSide Side, int Line, int? StartLine, string LineText, string Body);

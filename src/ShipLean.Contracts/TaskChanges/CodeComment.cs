namespace ShipLean.Contracts.TaskChanges;

// A top-level comment on a line (Repo, Sha, Path, Side and Line are set) or a reply (they are null; ParentId is set).
public sealed record CodeComment(
    Guid Id,
    Guid? ParentId,
    string? Repo,
    string? Sha,
    string? Path,
    CodeSide? Side,
    int? Line,
    int? StartLine,
    string? LineText,
    string Body,
    string BodyHtml,
    string By,
    bool ByAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? OutdatedAt,
    DateTimeOffset? SentBackAt,
    IReadOnlyList<CodeComment> Replies);

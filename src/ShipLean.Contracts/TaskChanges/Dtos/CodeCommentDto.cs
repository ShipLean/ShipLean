namespace ShipLean.Contracts.TaskChanges.Dtos;

// A line comment or a reply. A reply has a ParentId and null anchor fields. Side is "old" or "new". Replies is empty on a reply.
public sealed record CodeCommentDto(
    Guid Id,
    Guid? ParentId,
    string? Repo,
    string? Sha,
    string? Path,
    string? Side,
    int? Line,
    int? StartLine,
    string? LineText,
    string BodyHtml,
    string By,
    bool ByAgent,
    string CreatedAtLabel,
    bool Resolved,
    bool Outdated,
    bool SentBack,
    IReadOnlyList<CodeCommentDto> Replies);

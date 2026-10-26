using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Attachments;

// A file on a work item. DownloadUrl is "/api/attachments/{id}/content". UploadedBy is null for an import with no user.
// Kind comes from the name and content type. ReplacedCount is how many earlier files of the same name this one replaced.
// AgentName and Via are set when an agent attached it (Via is the MCP tool name), otherwise null.
public sealed record AttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string Name,
    string ContentType,
    long SizeBytes,
    UserRef? UploadedBy,
    DateTimeOffset CreatedAt,
    string CreatedLabel,
    string DownloadUrl,
    AttachmentKind Kind,
    int ReplacedCount,
    string? AgentName,
    string? Via);

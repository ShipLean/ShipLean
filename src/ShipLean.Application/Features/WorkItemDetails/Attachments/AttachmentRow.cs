using ShipLean.Application.Identity;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// An attachment joined to its uploader, as Columns reads it. The uploader columns are null for an import with no user.
internal sealed record AttachmentRow(
    Guid Id,
    Guid WorkItemId,
    string Name,
    string ContentType,
    long SizeBytes,
    string StorageRef,
    DateTimeOffset CreatedAt,
    Guid? UploaderId,
    string? UploaderName,
    string? UploaderInitials,
    string? UploaderColour,
    string? UploaderPhotoUrl,
    string? AgentName,
    string? UploadedVia,
    int ReplacedCount,
    bool Sanitized)
{
    public const string Columns =
        $"""
        a.id, a.work_item_id AS workItemId, a.name, a.content_type AS contentType, a.size_bytes AS sizeBytes, a.storage_ref AS storageRef, a.created_at AS createdAt,
        u.id AS uploaderId, u.name AS uploaderName, u.initials AS uploaderInitials, u.avatar_colour AS uploaderColour, {UserRefs.PhotoUrlSql} AS uploaderPhotoUrl,
        a.agent_name AS agentName, a.uploaded_via AS uploadedVia,
        (SELECT count(*)::int FROM attachment r WHERE r.work_item_id = a.work_item_id AND r.name = a.name AND r.archived_at IS NOT NULL) AS replacedCount,
        a.sanitized
        """;

    public const string From = "FROM attachment a LEFT JOIN app_user u ON u.id = a.uploaded_by";

    public AttachmentDto ToDto(TimeProvider clock)
        => new(
            Id, WorkItemId, Name, ContentType, SizeBytes,
            UploaderId is { } id ? new UserRef(id, UploaderName!, UploaderInitials!, UploaderColour!, UploaderPhotoUrl) : null,
            CreatedAt, DisplayDate.Relative(CreatedAt, clock), $"/api/attachments/{Id}/content",
            AttachmentKinds.Of(Name, ContentType),
            ReplacedCount,
            AgentName, UploadedVia);
}

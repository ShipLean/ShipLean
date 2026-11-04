namespace ShipLean.Application.Features.Demos.Reads;

// The sprint with its demo_ columns, its project's key and its demo item's key. This is the one place the demo_ columns are named for reading (K3).
internal sealed record SprintDemoRow(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    string Name,
    string? RolloverStatus,
    Guid? DemoWorkItemId,
    string? ItemKey,
    Guid? VideoAttachmentId,
    Guid? ScriptAttachmentId,
    Guid? ChaptersAttachmentId,
    decimal? DurationSeconds,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? UploadExpiresAt,
    DateTimeOffset? DeliveredAt,
    string? BlockedMessage,
    string YouTubeState,
    string? YouTubeVideoId,
    string? YouTubePrivacy,
    string? YouTubeFailure)
{
    public const string Columns =
        """
        s.id, s.project_id AS projectId, p.key AS projectKey, s.name, s.rollover_status AS rolloverStatus,
        s.demo_work_item_id AS demoWorkItemId, i.key AS itemKey,
        s.demo_video_attachment_id AS videoAttachmentId, s.demo_script_attachment_id AS scriptAttachmentId, s.demo_chapters_attachment_id AS chaptersAttachmentId,
        s.demo_duration_seconds AS durationSeconds, s.demo_requested_at AS requestedAt, s.demo_upload_expires_at AS uploadExpiresAt,
        s.demo_delivered_at AS deliveredAt, s.demo_blocked_message AS blockedMessage,
        s.demo_youtube_state AS youTubeState, s.demo_youtube_video_id AS youTubeVideoId, s.demo_youtube_privacy AS youTubePrivacy, s.demo_youtube_failure AS youTubeFailure
        """;

    public const string From = "FROM sprint s JOIN project p ON p.id = s.project_id LEFT JOIN work_item i ON i.id = s.demo_work_item_id";

    public bool Closed => RolloverStatus == "decided";
}

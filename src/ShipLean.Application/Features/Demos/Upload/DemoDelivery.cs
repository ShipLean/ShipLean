using System.Data.Common;
using System.Text;
using System.Text.Json;
using Dapper;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Mcp;

namespace ShipLean.Application.Features.Demos.Upload;

// K8. The three files are staged (bytes first, no row, no connection held), then committed with the sprint's pointers in one transaction under the
// item lock, so the old files and pointers stay live unless all of it lands. A retry that revoked the token before the commit wins: the hook refuses.
public sealed class DemoDelivery(IAttachmentStore attachments, ITransferTokens tokens, IProjectEvents events, AttachmentLimits limits) : IDemoDelivery
{
    public const string VideoName = "sprint-demo.mp4";
    public const string ScriptName = "sprint-demo-script.md";
    public const string ChaptersName = "sprint-demo-chapters.json";

    private static readonly JsonSerializerOptions ChapterOptions = new(JsonSerializerDefaults.Web);

    private sealed record SprintRow(Guid Id, Guid ProjectId, string Name, string ProjectKey, string BaseUrl);

    public async Task<DemoUploadedDto> DeliverAsync(DemoDeliveryParts parts, CancellationToken cancellationToken = default)
    {
        var grant = parts.Grant;
        var source = new AttachmentSource(grant.AgentStamp, "create_sprint_demo");
        var chapters = await StageAsync(grant, ChaptersName, "application/json", new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(parts.Chapters, ChapterOptions)), source, null, cancellationToken);
        var script = await StageAsync(grant, ScriptName, "text/markdown", new MemoryStream(Encoding.UTF8.GetBytes(parts.Script)), source, null, cancellationToken);
        var video = await StageAsync(grant, VideoName, "video/mp4", parts.Video, source, limits.MaxDemoBytes, cancellationToken);

        SprintRow? sprint = null;
        await attachments.CommitStagedAsync(
            grant.WorkItemId, [chapters, script, video],
            async (transaction, token) => sprint = await PointAsync(transaction, parts, chapters.Id, script.Id, video.Id, token),
            cancellationToken);

        await events.PublishAsync(new SprintDemoChanged(sprint!.ProjectId, sprint.Id));
        return new DemoUploadedDto(
            sprint.Name, video.Id, video.SizeBytes, parts.Chapters.Chapters.Count, McpOpenUrls.SprintDemo(sprint.BaseUrl, sprint.ProjectKey, sprint.Id));
    }

    private Task<StagedAttachment> StageAsync(
        TransferGrant grant, string name, string contentType, Stream content, AttachmentSource source, long? maxBytes, CancellationToken cancellationToken)
        => attachments.StageAsync(grant.WorkItemId, name, contentType, content, source, maxBytes, cancellationToken);

    // Runs on the commit's transaction, after the item row lock. Zero rows (the sprint no longer points at this item) throws, so every new row rolls back.
    private async Task<SprintRow> PointAsync(DbTransaction transaction, DemoDeliveryParts parts, Guid chaptersId, Guid scriptId, Guid videoId, CancellationToken cancellationToken)
    {
        if (await tokens.IsRevokedAsync(parts.Token, transaction, cancellationToken))
        {
            throw ApiException.NotFound("That upload link is not valid.");
        }

        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<SprintRow>(new CommandDefinition(
            """
            UPDATE sprint s SET
                demo_video_attachment_id = @videoId,
                demo_script_attachment_id = @scriptId,
                demo_chapters_attachment_id = @chaptersId,
                demo_duration_seconds = @duration,
                demo_delivered_at = now(),
                demo_upload_expires_at = NULL,
                demo_youtube_state = CASE WHEN EXISTS (
                    SELECT 1 FROM project_youtube_channel c WHERE c.project_id = s.project_id AND c.status = 'connected' AND c.archived_at IS NULL)
                    THEN 'pending' ELSE 'none' END,
                demo_youtube_video_id = NULL,
                demo_youtube_privacy = NULL,
                demo_youtube_failure = NULL,
                demo_youtube_attempted_at = NULL
            WHERE s.demo_work_item_id = @itemId
            RETURNING s.id AS Id, s.project_id AS ProjectId, s.name AS Name,
                (SELECT p.key FROM project p WHERE p.id = s.project_id) AS ProjectKey,
                (SELECT i.base_url FROM instance_settings i LIMIT 1) AS BaseUrl
            """,
            new { videoId, scriptId, chaptersId, duration = parts.Chapters.DurationSeconds, itemId = parts.Grant.WorkItemId }, transaction, cancellationToken: cancellationToken));
        return row ?? throw new InvalidOperationException("No sprint points at this demo item, so the delivery can't move its pointers.");
    }
}

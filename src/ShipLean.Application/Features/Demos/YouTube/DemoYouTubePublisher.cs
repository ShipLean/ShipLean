using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Demos.YouTube;

// The hosted loop that forwards kept demo videos to YouTube, every 30 seconds (K12). A pass claims one pending sprint at a time with FOR UPDATE SKIP LOCKED
// and stamps its attempt, so a second runner skips it; the lock lasts only as long as the claim, not the upload, so nothing else waits on the sprint. A stamp
// older than the lease means the runner that took it died, and the sprint is claimed again. A pass that fails is logged rather than allowed to stop the host.
// This has been exercised against the fake and the stub, not against Google.
public sealed class DemoYouTubePublisher(
    NpgsqlDataSource dataSource, IServiceScopeFactory scopes, IProjectEvents events, TimeProvider time, ILogger<DemoYouTubePublisher> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ReleaseLimit = TimeSpan.FromSeconds(10);
    private const string UnknownFailure = "ShipLean could not send the video to YouTube. Try again.";

    private sealed record Claimed(Guid Id, Guid ProjectId, string ProjectName, string SprintName, Guid? ItemId, Guid? VideoId, Guid? ChaptersId);

    private sealed record Channel(string RefreshToken, string Visibility);

    private sealed record Sent(string VideoId, YouTubeVisibility Privacy);

    private sealed record File(Guid Id, long SizeBytes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every, time);
        try
        {
            do
            {
                await PassAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    // Forwards every pending sprint that is due and returns how many were published. A failure of one is recorded on it and does not stop the rest.
    public async Task<int> PublishDueAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        while (await ClaimAsync(cancellationToken) is { } claimed)
        {
            try
            {
                if (await ForwardAsync(claimed, cancellationToken))
                {
                    published++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ReleaseAsync(claimed);
                throw;
            }
            catch (Exception failed)
            {
                // Whatever went wrong, the sprint must not be picked up again by itself: it ends failed, and a person retries it.
                logger.LogError(failed, "Forwarding the demo of sprint {SprintId} to YouTube failed", claimed.Id);
                await FinishAsync(claimed, "failed", UnknownFailure, null, null, cancellationToken);
            }
        }

        return published;
    }

    private async Task PassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await PublishDueAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Forwarding demo videos to YouTube failed");
        }
    }

    private async Task<Claimed?> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Claimed>(new CommandDefinition(
            """
            UPDATE sprint s SET demo_youtube_attempted_at = @now
            WHERE s.id = (
                SELECT d.id FROM sprint d
                WHERE d.demo_youtube_state = 'pending' AND d.archived_at IS NULL AND (d.demo_youtube_attempted_at IS NULL OR d.demo_youtube_attempted_at < @stale)
                ORDER BY d.demo_youtube_attempted_at NULLS FIRST, d.id
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING s.id, s.project_id AS ProjectId, (SELECT p.name FROM project p WHERE p.id = s.project_id) AS ProjectName, s.name AS SprintName,
                      s.demo_work_item_id AS ItemId, s.demo_video_attachment_id AS VideoId, s.demo_chapters_attachment_id AS ChaptersId
            """,
            new { now, stale = now - Lease }, cancellationToken: cancellationToken));
    }

    // Returns whether the video was published.
    private async Task<bool> ForwardAsync(Claimed claimed, CancellationToken cancellationToken)
    {
        var channel = await ChannelAsync(claimed.ProjectId, cancellationToken);
        if (channel is null)
        {
            await FinishAsync(claimed, "none", null, null, null, cancellationToken);
            return false;
        }

        using var scope = scopes.CreateScope();
        var files = await FilesAsync(claimed, cancellationToken);
        if (claimed.VideoId is not { } videoId || !files.TryGetValue(videoId, out var video))
        {
            await FinishAsync(claimed, "failed", "The demo video is no longer on the work item, so there is nothing to send to YouTube. Upload the demo again.", null, null, cancellationToken);
            return false;
        }

        try
        {
            var sent = await SendAsync(scope.ServiceProvider, claimed, channel, video, files, cancellationToken);
            await FinishAsync(claimed, "published", null, sent.VideoId, sent.Privacy.ToString().ToLowerInvariant(), cancellationToken);
            logger.LogInformation("The demo of sprint {SprintId} was forwarded to YouTube as {YouTubeVideoId}", claimed.Id, sent.VideoId);
            return true;
        }
        catch (YouTubeFailureException failure)
        {
            logger.LogWarning(failure, "Forwarding the demo of sprint {SprintId} to YouTube failed ({Kind})", claimed.Id, failure.Kind);
            await FinishAsync(claimed, "failed", failure.Message, null, null, cancellationToken);
            return false;
        }
    }

    private async Task<Sent> SendAsync(
        IServiceProvider services, Claimed claimed, Channel channel, File video, IReadOnlyDictionary<Guid, File> files, CancellationToken cancellationToken)
    {
        var settings = await services.GetRequiredService<IYouTubeSettings>().GetAsync(cancellationToken)
            ?? throw new YouTubeFailureException(YouTubeFailureKind.Rejected, "YouTube is not set up on this instance.");
        var attachments = services.GetRequiredService<IAttachmentStore>();
        var client = services.GetRequiredService<IYouTubeClient>();

        var description = DemoYouTubeDescription.Of(await ChaptersAsync(attachments, claimed.ChaptersId, files, cancellationToken));
        var visibility = Enum.Parse<YouTubeVisibility>(channel.Visibility, ignoreCase: true);
        var accessToken = await client.AccessTokenAsync(settings, services.GetRequiredService<IntegrationTokens>().Unprotect(channel.RefreshToken), cancellationToken);

        string videoId;
        var upload = new YouTubeUpload(DemoYouTubeDescription.Title(claimed.ProjectName, claimed.SprintName), description, visibility, "video/mp4");
        try
        {
            var (_, content) = await attachments.OpenAsync(video.Id, cancellationToken);
            await using (content.Content)
            {
                videoId = await client.UploadAsync(accessToken, upload, content.Content, video.SizeBytes, cancellationToken);
            }
        }
        catch (ApiException gone) when (gone.Code == ApiErrorCodes.NotFound)
        {
            throw new YouTubeFailureException(YouTubeFailureKind.Rejected, "The demo video is no longer on the work item, so there is nothing to send to YouTube. Upload the demo again.");
        }

        try
        {
            return new Sent(videoId, await client.ReadPrivacyAsync(accessToken, videoId, cancellationToken));
        }
        catch (YouTubeFailureException readBack)
        {
            // Uploaded but unread: what YouTube made of it is unknown, so the video is taken down and a retry starts clean.
            throw new YouTubeFailureException(readBack.Kind, $"{readBack.Message} {await TakeDownAsync(client, accessToken, videoId)}");
        }
    }

    // Runs on its own limit so a stopping host still removes the video. Says what to tell the person.
    private async Task<string> TakeDownAsync(IYouTubeClient client, string accessToken, string videoId)
    {
        using var limit = new CancellationTokenSource(ReleaseLimit, time);
        try
        {
            await client.DeleteAsync(accessToken, videoId, limit.Token);
            return "The video was uploaded but could not be checked, so it was removed from the channel; retry to send it again.";
        }
        catch (Exception failed) when (failed is YouTubeFailureException or OperationCanceledException)
        {
            return $"The video {videoId} was uploaded but could not be checked, and it could not be removed from the channel. Delete it in YouTube Studio before retrying.";
        }
    }

    private static async Task<string?> ChaptersAsync(IAttachmentStore attachments, Guid? chaptersId, IReadOnlyDictionary<Guid, File> files, CancellationToken cancellationToken)
    {
        if (chaptersId is not { } id || !files.ContainsKey(id))
        {
            return null;
        }

        try
        {
            var (_, content) = await attachments.OpenAsync(id, cancellationToken);
            await using (content.Content)
            {
                using var reader = new StreamReader(content.Content);
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        catch (ApiException gone) when (gone.Code == ApiErrorCodes.NotFound)
        {
            return null;
        }
    }

    // Only a connected channel with a visibility can be sent to; the channel is read again here, so a disconnect since the delivery is seen.
    private async Task<Channel?> ChannelAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Channel>(new CommandDefinition(
            """
            SELECT refresh_token AS RefreshToken, visibility FROM project_youtube_channel
            WHERE project_id = @projectId AND archived_at IS NULL AND status = 'connected' AND visibility IS NOT NULL AND refresh_token <> ''
            """,
            new { projectId }, cancellationToken: cancellationToken));
    }

    // Only the live files of the demo's own item: a pointer to an archived file, or to another item's, reads as no file.
    private async Task<IReadOnlyDictionary<Guid, File>> FilesAsync(Claimed claimed, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. new[] { claimed.VideoId, claimed.ChaptersId }.OfType<Guid>()];
        if (claimed.ItemId is not { } itemId || ids.Length == 0)
        {
            return new Dictionary<Guid, File>();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<File>(new CommandDefinition(
            "SELECT id, size_bytes AS SizeBytes FROM attachment WHERE id = ANY(@ids) AND work_item_id = @itemId AND archived_at IS NULL",
            new { ids, itemId }, cancellationToken: cancellationToken))).ToDictionary(f => f.Id);
    }

    // Writes the outcome, only while the sprint still waits on the file this pass sent: a delivery that replaced it meanwhile has set its own state.
    private async Task FinishAsync(Claimed claimed, string state, string? failure, string? videoId, string? privacy, CancellationToken cancellationToken)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var saved = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sprint SET demo_youtube_state = @state, demo_youtube_failure = @failure, demo_youtube_video_id = @videoId,
                                  demo_youtube_privacy = @privacy, demo_youtube_attempted_at = @now
                WHERE id = @id AND demo_youtube_state = 'pending' AND demo_video_attachment_id IS NOT DISTINCT FROM @attachmentId
                """,
                new { claimed.Id, state, failure, videoId, privacy, now = time.GetUtcNow(), attachmentId = claimed.VideoId }, cancellationToken: cancellationToken));
            if (saved == 0)
            {
                logger.LogWarning("The demo of sprint {SprintId} changed while it was being forwarded to YouTube, so the outcome ({State}) was not recorded", claimed.Id, state);
                return;
            }
        }

        await events.PublishAsync(new SprintDemoChanged(claimed.ProjectId, claimed.Id));
    }

    // A host that is stopping gives the claim back, so the next one picks it up without waiting out the lease.
    private async Task ReleaseAsync(Claimed claimed)
    {
        using var limit = new CancellationTokenSource(ReleaseLimit, time);
        await using var connection = await dataSource.OpenConnectionAsync(limit.Token);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sprint SET demo_youtube_attempted_at = NULL WHERE id = @id AND demo_youtube_state = 'pending'", new { claimed.Id }, cancellationToken: limit.Token));
    }
}

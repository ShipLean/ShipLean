using Dapper;
using Npgsql;
using ShipLean.Application.Features.Instance.Integrations;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Core;

namespace ShipLean.Application.Features.Demos.YouTube;

// The check before a channel connects (K12). It uploads the embedded test video at the chosen visibility, reads back what YouTube made of it, and
// deletes it whatever happened. Only a read-back that equals the choice, and a delete that worked, saves the channel as connected; every other
// outcome leaves it pending with the reason. The channel row is the only thing written, and only after the video is gone or the delete is known to have failed.
// This has been exercised against the fake and the stub, not against Google.
public sealed class YouTubeCheck(
    NpgsqlDataSource dataSource, IYouTubeClient client, IYouTubeSettings settings, IntegrationTokens tokens, TimeProvider clock) : IYouTubeCheck
{
    private const string Resource = "sprint-demo/youtube-check.mp4";
    private const string UploadTitle = "ShipLean connection check";
    private const string Description = "A test video ShipLean made while connecting this channel. It is deleted straight away.";
    private static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(30);

    private sealed record Channel(Guid Id, string Title, string RefreshToken);

    private sealed record Outcome(bool Passed, string? Refusal);

    public async Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default)
    {
        var app = await settings.GetAsync(cancellationToken) ?? throw ApiException.NotFound("YouTube is not set up on this instance.");
        var channel = await LoadAsync(projectId, cancellationToken) ?? throw ApiException.Conflict("There is no YouTube channel to check.");

        var outcome = await RunAsync(app, tokens.Unprotect(channel.RefreshToken), visibility, cancellationToken);

        var checkedAt = clock.GetUtcNow();
        await SaveAsync(channel.Id, outcome, visibility, checkedAt, cancellationToken);
        return new YouTubeProjectDto(
            outcome.Passed ? YouTubeConnectionStatus.Connected : YouTubeConnectionStatus.Pending,
            channel.Title,
            outcome.Passed ? visibility : null,
            $"Checked {DisplayDate.Relative(checkedAt, clock)}",
            outcome.Refusal);
    }

    private async Task<Outcome> RunAsync(YouTubeClientSettings app, string refreshToken, YouTubeVisibility chosen, CancellationToken cancellationToken)
    {
        string? videoId = null;
        string? accessToken = null;
        string? refusal = null;
        string? leftBehind = null;
        try
        {
            accessToken = await client.AccessTokenAsync(app, refreshToken, cancellationToken);
            videoId = await UploadAsync(accessToken, chosen, cancellationToken);
            var cameBack = await client.ReadPrivacyAsync(accessToken, videoId, cancellationToken);
            refusal = cameBack == chosen ? null : Mismatch(chosen, cameBack);
        }
        catch (YouTubeFailureException failure)
        {
            refusal = failure.Message;
        }
        finally
        {
            if (videoId is not null)
            {
                leftBehind = await DeleteAsync(accessToken!, videoId);
            }
        }

        if (leftBehind is not null)
        {
            refusal = refusal is null ? leftBehind : $"{refusal} {leftBehind}";
        }
        return new Outcome(refusal is null, refusal);
    }

    private async Task<string> UploadAsync(string accessToken, YouTubeVisibility chosen, CancellationToken cancellationToken)
    {
        await using var video = typeof(YouTubeCheck).Assembly.GetManifestResourceStream(Resource)!;
        return await client.UploadAsync(accessToken, new YouTubeUpload(UploadTitle, Description, chosen, "video/mp4"), video, video.Length, cancellationToken);
    }

    // Runs on its own token so a request that was cancelled mid-check still removes the video. Returns what to tell the person when it could not.
    private async Task<string?> DeleteAsync(string accessToken, string videoId)
    {
        using var limit = new CancellationTokenSource(CleanupLimit, clock);
        try
        {
            await client.DeleteAsync(accessToken, videoId, limit.Token);
            return null;
        }
        catch (Exception failed) when (failed is YouTubeFailureException or OperationCanceledException)
        {
            var why = failed is YouTubeFailureException failure ? failure.Message : "YouTube did not answer in time.";
            return $"The test video {videoId} could not be deleted from the channel ({why}) Delete it in YouTube Studio, then check again.";
        }
    }

    private static string Mismatch(YouTubeVisibility chosen, YouTubeVisibility cameBack)
        => $"YouTube gave the test video {Word(cameBack)} visibility, not the {Word(chosen)} you chose. The channel or its account settings decide this. "
            + $"Change the channel's setting and check again, choose {Word(cameBack)}, or don't use YouTube.";

    private static string Word(YouTubeVisibility visibility) => visibility.ToString().ToLowerInvariant();

    private async Task<Channel?> LoadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Channel>(new CommandDefinition(
            "SELECT id, channel_title AS Title, refresh_token AS RefreshToken FROM project_youtube_channel WHERE project_id = @projectId AND archived_at IS NULL",
            new { projectId }, cancellationToken: cancellationToken));
    }

    // A pass connects at the chosen visibility. Anything else leaves the channel pending with the reason, a connected one included: its stored
    // visibility is no longer what was last chosen, so it must pass again. Written on the caller's token only after the cleanup above has finished.
    private async Task SaveAsync(Guid channelId, Outcome outcome, YouTubeVisibility chosen, DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var saved = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE project_youtube_channel
            SET status = @status, visibility = @visibility, refusal = @refusal, checked_at = @checkedAt
            WHERE id = @channelId AND archived_at IS NULL
            """,
            new
            {
                channelId,
                status = outcome.Passed ? "connected" : "pending",
                visibility = outcome.Passed ? Word(chosen) : null,
                refusal = outcome.Refusal,
                checkedAt,
            },
            cancellationToken: cancellationToken));
        if (saved == 0)
        {
            throw ApiException.Conflict("The YouTube channel was disconnected while it was being checked.");
        }
    }
}

using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Application.Integrations;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.YouTube;

// Connecting a project's own YouTube channel (K12): the consent URL, the callback, reading the channel's state and disconnecting.
// The callback is an anonymous route, so everything about who may connect is checked here. Nothing here saves a channel as
// connected: only the check does. No token, code or state is ever logged or put in a message.
public sealed class YouTubeConnect(
    NpgsqlDataSource dataSource,
    IYouTubeSettings settings,
    IYouTubeClient youtube,
    IntegrationTokens tokens,
    YouTubeConnectState states,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<YouTubeConnect> logger) : IYouTubeConnect
{
    public const string CallbackPath = "/api/youtube/callback";
    private const string ErrorHome = "/?youtube=error";
    private const string RevocationNotConfirmed = "Google did not confirm the revocation. Remove ShipLean's access in your Google account.";

    private sealed record ReadRow(string Status, string ChannelTitle, string? Visibility, DateTimeOffset? CheckedAt, string? Refusal);

    private sealed record LiveToken(Guid Id, string RefreshToken);

    public async Task<YouTubeProjectDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (await settings.GetAsync(cancellationToken) is null)
        {
            return new YouTubeProjectDto(YouTubeConnectionStatus.Off, null, null, null, null);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ReadRow>(new CommandDefinition(
            """
            SELECT status AS Status, channel_title AS ChannelTitle, visibility AS Visibility, checked_at AS CheckedAt, refusal AS Refusal
            FROM project_youtube_channel WHERE project_id = @projectId AND archived_at IS NULL
            """,
            new { projectId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return new YouTubeProjectDto(YouTubeConnectionStatus.NotConnected, null, null, null, null);
        }

        var visibility = Enum.TryParse<YouTubeVisibility>(row.Visibility, ignoreCase: true, out var parsed) ? parsed : (YouTubeVisibility?)null;
        if (row.Status != "connected")
        {
            return new YouTubeProjectDto(YouTubeConnectionStatus.Pending, row.ChannelTitle, visibility, null, row.Refusal);
        }

        string? label = null;
        if (row.CheckedAt is { } at)
        {
            label = $"Checked {SprintDemoLabels.When(at, time)}";
        }

        return new YouTubeProjectDto(YouTubeConnectionStatus.Connected, row.ChannelTitle, visibility, label, null);
    }

    public async Task<YouTubeStartDto> StartAsync(Guid projectId, string origin, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(cancellationToken);
        var state = states.Seal(currentUser.Id, projectId, time.GetUtcNow() + YouTubeConnectState.Lifetime);
        return new YouTubeStartDto(youtube.AuthorizationUrl(client, await RedirectUriAsync(origin, cancellationToken), state));
    }

    public async Task<string> CallbackAsync(Guid? signedInUser, string? code, string? state, string? error, string origin, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(cancellationToken);

        // Who may connect is decided before anything Google said is looked at, and every refusal here is the same redirect, so a
        // stranger learns nothing about the project.
        var grant = states.Open(state);
        if (grant is null || grant.ExpiresAt <= time.GetUtcNow() || signedInUser != grant.UserId)
        {
            return ErrorHome;
        }

        var key = await AdminProjectKeyAsync(grant.ProjectId, grant.UserId, cancellationToken);
        if (key is null)
        {
            return ErrorHome;
        }

        var settingsPath = $"/p/{Uri.EscapeDataString(key)}/settings/demos";
        if (!string.IsNullOrEmpty(error))
        {
            return $"{settingsPath}?youtube={(error == "access_denied" ? "denied" : "error")}";
        }

        var failed = $"{settingsPath}?youtube=error";
        if (string.IsNullOrEmpty(code))
        {
            return failed;
        }

        var redirectUri = await RedirectUriAsync(origin, cancellationToken);
        YouTubeTokens granted;
        try
        {
            granted = await youtube.ExchangeCodeAsync(client, code, redirectUri, cancellationToken);
        }
        catch (YouTubeFailureException failure)
        {
            logger.LogWarning(failure, "YouTube refused the code exchange for project {ProjectId}: {Kind}", grant.ProjectId, failure.Kind);
            return failed;
        }

        if (string.IsNullOrEmpty(granted.RefreshToken))
        {
            // Google gave no refresh token, so the connection could not outlive the access token. Google's revoke takes either kind.
            logger.LogWarning("YouTube gave no refresh token for project {ProjectId}", grant.ProjectId);
            await RevokeAsync(client, granted.AccessToken, cancellationToken);
            return failed;
        }

        if (!granted.GrantedScopes.Contains(GoogleYouTubeClient.Scope, StringComparer.Ordinal))
        {
            logger.LogWarning("YouTube consent for project {ProjectId} came back without the scope ShipLean needs", grant.ProjectId);
            await RevokeAsync(client, granted.RefreshToken, cancellationToken);
            return failed;
        }

        YouTubeChannel channel;
        try
        {
            channel = await youtube.ChannelAsync(granted.AccessToken, cancellationToken);
        }
        catch (YouTubeFailureException failure)
        {
            logger.LogWarning(failure, "YouTube would not name the channel for project {ProjectId}: {Kind}", grant.ProjectId, failure.Kind);
            await RevokeAsync(client, granted.RefreshToken, cancellationToken);
            return failed;
        }

        IReadOnlyList<string> replaced;
        try
        {
            replaced = await ReplaceAsync(grant, channel, tokens.Protect(granted.RefreshToken), cancellationToken);
        }
        catch (PostgresException conflict) when (conflict.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            logger.LogWarning(conflict, "Another YouTube connection for project {ProjectId} landed first", grant.ProjectId);
            await RevokeAsync(client, granted.RefreshToken, cancellationToken);
            return failed;
        }

        foreach (var old in replaced)
        {
            await RevokeStoredAsync(client, old, grant.ProjectId, cancellationToken);
        }

        return $"{settingsPath}?youtube=pending";
    }

    public async Task<YouTubeDisconnectDto> DisconnectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var live = await connection.QuerySingleOrDefaultAsync<LiveToken>(new CommandDefinition(
            "SELECT id AS Id, refresh_token AS RefreshToken FROM project_youtube_channel WHERE project_id = @projectId AND archived_at IS NULL",
            new { projectId }, cancellationToken: cancellationToken));
        if (live is null)
        {
            return new YouTubeDisconnectDto(true, null);
        }

        // Revoke first, then always archive and clear the token: a failed revocation still disconnects, because ShipLean would have no token to retry with.
        var revoked = await RevokeStoredAsync(client, live.RefreshToken, projectId, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project_youtube_channel SET archived_at = @now, refresh_token = '' WHERE id = @id AND archived_at IS NULL",
            new { now = time.GetUtcNow(), live.Id }, cancellationToken: cancellationToken));
        return revoked ? new YouTubeDisconnectDto(true, null) : new YouTubeDisconnectDto(false, RevocationNotConfirmed);
    }

    private async Task<YouTubeClientSettings> RequireClientAsync(CancellationToken cancellationToken)
        => await settings.GetAsync(cancellationToken) ?? throw ApiException.NotFound("YouTube isn't set up on this instance.");

    // The same URI goes to Google twice (the consent and the code exchange), so both come through here.
    private async Task<string> RedirectUriAsync(string origin, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var baseUrl = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        return (string.IsNullOrWhiteSpace(baseUrl) ? origin : baseUrl).TrimEnd('/') + CallbackPath;
    }

    private async Task<string?> AdminProjectKeyAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT p.key FROM project p
            JOIN project_member m ON m.project_id = p.id AND m.user_id = @userId AND m.role = 'admin' AND m.archived_at IS NULL
            WHERE p.id = @projectId
            """,
            new { projectId, userId }, cancellationToken: cancellationToken));
    }

    // Archives the project's live channel, if any, and stores the new one as pending. Returns the replaced channels' protected tokens, to revoke.
    private async Task<IReadOnlyList<string>> ReplaceAsync(YouTubeConnectState.Grant grant, YouTubeChannel channel, string protectedToken, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var old = (await connection.QueryAsync<LiveToken>(new CommandDefinition(
            "SELECT id AS Id, refresh_token AS RefreshToken FROM project_youtube_channel WHERE project_id = @projectId AND archived_at IS NULL FOR UPDATE",
            new { projectId = grant.ProjectId }, transaction, cancellationToken: cancellationToken))).ToList();
        var now = time.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project_youtube_channel SET archived_at = @now, refresh_token = '' WHERE project_id = @projectId AND archived_at IS NULL",
            new { now, projectId = grant.ProjectId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO project_youtube_channel (id, project_id, status, channel_id, channel_title, refresh_token, connected_by, created_at)
            VALUES (@id, @projectId, 'pending', @channelId, @channelTitle, @refreshToken, @userId, @now)
            """,
            new { id = Guid.CreateVersion7(), projectId = grant.ProjectId, channelId = channel.Id, channelTitle = channel.Title, refreshToken = protectedToken, userId = grant.UserId, now },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return [.. old.Select(row => row.RefreshToken).Where(token => token.Length > 0)];
    }

    // Revokes a token we hold in clear. A failure is logged by kind only and returned, never thrown.
    private async Task<bool> RevokeAsync(YouTubeClientSettings client, string token, CancellationToken cancellationToken)
    {
        try
        {
            await youtube.RevokeAsync(client, token, cancellationToken);
            return true;
        }
        catch (YouTubeFailureException failure)
        {
            logger.LogWarning(failure, "YouTube did not confirm a revocation: {Kind}", failure.Kind);
            return false;
        }
    }

    // Revokes a stored (protected) token. One that can no longer be opened, or that is already empty, cannot be revoked.
    private async Task<bool> RevokeStoredAsync(YouTubeClientSettings client, string protectedToken, Guid projectId, CancellationToken cancellationToken)
    {
        if (protectedToken.Length == 0)
        {
            return false;
        }

        string token;
        try
        {
            token = tokens.Unprotect(protectedToken);
        }
        catch (ApiException unreadable)
        {
            logger.LogWarning(unreadable, "A saved YouTube token for project {ProjectId} can no longer be read", projectId);
            return false;
        }

        return await RevokeAsync(client, token, cancellationToken);
    }
}

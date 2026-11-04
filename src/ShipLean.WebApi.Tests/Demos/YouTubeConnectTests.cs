using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Fakes.YouTube;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Application.Integrations;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Demos;

// Y3 over the real routes, service and database: the consent URL with its signed state, the anonymous callback and everything it refuses, and
// disconnecting with Google's access revoked. Google is the FakeYouTubeClient behind a recording wrapper; the instance's OAuth client is a settable double.
// One class, one host: the tests run in order on the one database and each starts by archiving the channels left by the one before.
[TestFixture]
public class YouTubeConnectTests : ApiFixture
{
    private const string Code = "auth-code-7f3a91c2";
    private const string ClientId = "connect-client.apps.example";
    private const string ClientSecret = "connect-client-secret-55aa";
    private static readonly string Project = $"/api/projects/{SeedIds.Projects.Ship}/youtube";
    private static readonly Regex Settled = new(@"^(/|/p/SHIP/settings/demos)\?youtube=(pending|denied|error)$", RegexOptions.None, TimeSpan.FromSeconds(2));

    private readonly FakeYouTubeClient _fake = new();
    private readonly SettableSettings _settings = new();
    private readonly RecordedLogs _logs = new();
    private TrackingClient _client = null!;
    private HttpClient _browser = null!;
    private DateTimeOffset _since;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _client = new TrackingClient(_fake);
        services.RemoveAll<IYouTubeClient>();
        services.AddSingleton<IYouTubeClient>(_client);
        services.RemoveAll<IYouTubeSettings>();
        services.AddSingleton<IYouTubeSettings>(_settings);
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_logs);
    };

    [OneTimeSetUp]
    public void StartBrowser() => _browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(Host.BaseUrl) };

    [OneTimeTearDown]
    public void StopBrowser() => _browser.Dispose();

    [SetUp]
    public async Task Reset()
    {
        _settings.Client = new YouTubeClientSettings(ClientId, ClientSecret);
        _fake.NarrowGrantedScope = false;
        _fake.RevokeFails = false;
        _client.DropRefreshToken = false;
        _client.Forget();
        _since = Now();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE project_youtube_channel SET archived_at = now(), refresh_token = '' WHERE archived_at IS NULL");
        await database.ExecuteAsync("UPDATE project_member SET role = 'admin' WHERE project_id = @project AND user_id = @user", new { project = SeedIds.Projects.Ship, user = SeedIds.Users.Joel });
        await database.ExecuteAsync("UPDATE project_member SET role = 'member' WHERE project_id = @project AND user_id = @user", new { project = SeedIds.Projects.Ship, user = SeedIds.Users.Priya });
    }

    // ---- Start

    [Test]
    public async Task Start_WhenAdmin_ShouldGiveAUrlWithSignedState()
    {
        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/start");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var url = (await Json(response))["url"]!.GetValue<string>();
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        query["client_id"].ToString().ShouldBe(ClientId);
        query["redirect_uri"].ToString().ShouldBe($"{await BaseUrlAsync()}/api/youtube/callback");
        url.ShouldNotContain(ClientSecret);
        var grant = States().Open(query["state"].ToString());
        grant.ShouldNotBeNull();
        grant.UserId.ShouldBe(SeedIds.Users.Joel);
        grant.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        grant.ExpiresAt.ShouldBeInRange(Now().AddMinutes(9), Now().AddMinutes(11));
    }

    [Test]
    public async Task Start_WhenNoBaseUrl_ShouldUseTheRequestOrigin()
    {
        await SetBaseUrlAsync(string.Empty);
        try
        {
            using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/start");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var query = QueryHelpers.ParseQuery(new Uri((await Json(response))["url"]!.GetValue<string>()).Query);
            query["redirect_uri"].ToString().ShouldBe($"{Host.BaseUrl.TrimEnd('/')}/api/youtube/callback");
        }
        finally
        {
            await SetBaseUrlAsync(await ReadSeedBaseUrlAsync());
        }
    }

    [Test]
    public async Task Start_WhenOff_ShouldBeNotFound()
    {
        _settings.Client = null;

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/start");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Start_WhenMember_ShouldBeForbidden()
    {
        using var response = await Api.ActingAs(TestUser.Priya).PostJsonAsync($"{Project}/start");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    // ---- Callback

    [Test]
    public async Task Callback_WhenGood_ShouldStorePendingAndRedirect()
    {
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(SeedIds.Users.Joel, state);

        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=pending");
        var rows = await RowsAsync();
        var row = rows.ShouldHaveSingleItem();
        row.Status.ShouldBe("pending");
        row.ChannelId.ShouldBe("UC-fake-channel");
        row.ChannelTitle.ShouldBe("Fake channel");
        row.Visibility.ShouldBeNull();
        row.CheckedAt.ShouldBeNull();
        row.Refusal.ShouldBeNull();
        row.ArchivedAt.ShouldBeNull();
        row.ConnectedBy.ShouldBe(SeedIds.Users.Joel);
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.Pending);
    }

    [Test]
    public async Task Callback_WhenOff_ShouldBeNotFound()
    {
        var state = await StartStateAsync(TestUser.Joel);
        _settings.Client = null;

        using var response = await CallbackAsync(SeedIds.Users.Joel, state);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenDenied_ShouldRedirectDeniedAndStoreNothing()
    {
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(SeedIds.Users.Joel, state, code: null, error: "access_denied");

        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=denied");
        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenStateTampered_ShouldRedirectHomeAndStoreNothing()
    {
        var state = await StartStateAsync(TestUser.Joel);
        var middle = state.Length / 2;
        var flipped = state[..middle] + (state[middle] == 'A' ? 'B' : 'A') + state[(middle + 1)..];
        var foreign = Host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("another-purpose")
            .Protect($"{SeedIds.Users.Joel:N}|{SeedIds.Projects.Ship:N}|{Now().AddMinutes(5).ToUnixTimeSeconds()}");

        foreach (var bad in new[] { flipped, state[..^4], foreign, "not-a-state", string.Empty })
        {
            using var response = await CallbackAsync(SeedIds.Users.Joel, bad);
            Redirect(response).ShouldBe("/?youtube=error");
        }

        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenStateExpired_ShouldStoreNothing()
    {
        var expired = States().Seal(SeedIds.Users.Joel, SeedIds.Projects.Ship, Now().AddMinutes(-1));

        using var response = await CallbackAsync(SeedIds.Users.Joel, expired);

        Redirect(response).ShouldBe("/?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenOtherUsersCookie_ShouldStoreNothing()
    {
        // Priya is an admin of the project too, so only the match between the state's user and the signed-in one can refuse her.
        await SetRoleAsync(SeedIds.Users.Priya, "admin");
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(SeedIds.Users.Priya, state);

        Redirect(response).ShouldBe("/?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenSignedOut_ShouldRedirectErrorAndStoreNothing()
    {
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(null, state);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        Redirect(response).ShouldBe("/?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenAdminLostRole_ShouldStoreNothing()
    {
        var state = await StartStateAsync(TestUser.Joel);
        await SetRoleAsync(SeedIds.Users.Joel, "member");

        using var response = await CallbackAsync(SeedIds.Users.Joel, state);

        Redirect(response).ShouldBe("/?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        _client.Granted.ShouldBeEmpty();
    }

    [Test]
    public async Task Callback_WhenNoRefreshToken_ShouldStoreNothing()
    {
        _client.DropRefreshToken = true;
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(SeedIds.Users.Joel, state);

        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        // What Google did hand out is revoked rather than left behind.
        _fake.Revocations.ShouldContain(_client.Latest.AccessToken);
    }

    [Test]
    public async Task Callback_WhenScopeNarrowed_ShouldRevokeAndStoreNothing()
    {
        _fake.NarrowGrantedScope = true;
        var state = await StartStateAsync(TestUser.Joel);

        using var response = await CallbackAsync(SeedIds.Users.Joel, state);

        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=error");
        (await RowsAsync()).ShouldBeEmpty();
        _fake.Revocations.ShouldContain(_client.Latest.RefreshToken);
    }

    [Test]
    public async Task Callback_ShouldOnlyRedirectToTheProjectsSettings()
    {
        const string Evil = "https://evil.example/steal";
        var good = await StartStateAsync(TestUser.Joel);
        var outcomes = new List<string>();

        foreach (var (user, state, code, error) in new (Guid?, string?, string?, string?)[]
        {
            (SeedIds.Users.Joel, good, Evil, null),
            (SeedIds.Users.Joel, await StartStateAsync(TestUser.Joel), null, Evil),
            (SeedIds.Users.Joel, await StartStateAsync(TestUser.Joel), Code, "access_denied"),
            (SeedIds.Users.Joel, Evil, Code, null),
            (null, await StartStateAsync(TestUser.Joel), Code, Evil),
        })
        {
            using var request = Browser(user, state, code, error);
            request.Headers.Add("X-Forwarded-Host", "evil.example");
            request.Headers.Add("Origin", Evil);
            using var response = await _browser.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.Found);
            outcomes.Add(Redirect(response));
        }

        foreach (var location in outcomes)
        {
            Settled.IsMatch(location).ShouldBeTrue(location);
            location.ShouldNotContain("evil");
        }

        outcomes[0].ShouldBe("/p/SHIP/settings/demos?youtube=pending");
        outcomes[1].ShouldBe("/p/SHIP/settings/demos?youtube=error");
        outcomes[2].ShouldBe("/p/SHIP/settings/demos?youtube=denied");
    }

    [Test]
    public async Task RefreshToken_ShouldBeProtectedAtRest()
    {
        var state = await StartStateAsync(TestUser.Joel);
        using var response = await CallbackAsync(SeedIds.Users.Joel, state);
        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=pending");

        var raw = _client.Latest.RefreshToken;
        var stored = (await RowsAsync()).ShouldHaveSingleItem().RefreshToken;

        stored.ShouldNotBeEmpty();
        stored.ShouldNotBe(raw);
        stored.ShouldNotContain(raw);
        Host.Services.GetRequiredService<IntegrationTokens>().Unprotect(stored).ShouldBe(raw);
    }

    [Test]
    public async Task Logs_ShouldHoldNoTokenOrCode()
    {
        var states = new List<string> { await StartStateAsync(TestUser.Joel) };
        await CallbackQuietlyAsync(SeedIds.Users.Joel, states[0]);

        _fake.NarrowGrantedScope = true;
        states.Add(await StartStateAsync(TestUser.Joel));
        await CallbackQuietlyAsync(SeedIds.Users.Joel, states[1]);

        _fake.NarrowGrantedScope = false;
        _client.DropRefreshToken = true;
        states.Add(await StartStateAsync(TestUser.Joel));
        await CallbackQuietlyAsync(SeedIds.Users.Joel, states[2]);

        _client.DropRefreshToken = false;
        await CallbackQuietlyAsync(SeedIds.Users.Joel, states[0] + "x");

        states.Add(await StartStateAsync(TestUser.Joel));
        await CallbackQuietlyAsync(SeedIds.Users.Joel, states[3]);

        _fake.RevokeFails = true;
        await DisconnectAsync();

        var events = _logs.Events;
        events.ShouldContain(line => line.Contains("YouTube") && line.Contains("Warning"));
        var secrets = new List<string> { Code, ClientSecret };
        secrets.AddRange(states);
        secrets.AddRange(_client.Issued.SelectMany(t => new[] { t.AccessToken, t.RefreshToken }).Where(t => t.Length > 0));
        secrets.AddRange((await RowsAsync()).Select(r => r.RefreshToken).Where(t => t.Length > 0));
        foreach (var secret in secrets)
        {
            events.ShouldNotContain(line => line.Contains(secret, StringComparison.Ordinal), secret);
        }
    }

    // ---- Disconnect and replace

    [Test]
    public async Task Disconnect_ShouldRevokeArchiveAndClearTheToken()
    {
        var token = await ConnectAsync();

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/disconnect");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        body["revoked"]!.GetValue<bool>().ShouldBeTrue();
        body["message"].ShouldBeNull();
        _fake.Revocations.ShouldContain(token);
        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.ArchivedAt.ShouldNotBeNull();
        row.RefreshToken.ShouldBeEmpty();
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.NotConnected);
    }

    [Test]
    public async Task Disconnect_WhenRevokeFails_ShouldStillArchiveClearAndSaySo()
    {
        var token = await ConnectAsync();
        _fake.RevokeFails = true;

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/disconnect");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(response);
        body["revoked"]!.GetValue<bool>().ShouldBeFalse();
        body["message"]!.GetValue<string>().ShouldContain("Google account");
        _fake.Revocations.ShouldNotContain(token);
        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.ArchivedAt.ShouldNotBeNull();
        row.RefreshToken.ShouldBeEmpty();
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.NotConnected);
    }

    [Test]
    public async Task Disconnect_ThenConnect_ShouldAllowANewOne()
    {
        var first = await ConnectAsync();
        await DisconnectAsync();
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.NotConnected);

        var second = await ConnectAsync();

        second.ShouldNotBe(first);
        _fake.Revocations.Count(token => token == first).ShouldBe(1);
        var rows = await RowsAsync();
        rows.Count.ShouldBe(2);
        rows.Count(r => r.ArchivedAt is null).ShouldBe(1);
        var live = rows.Single(r => r.ArchivedAt is null);
        live.Status.ShouldBe("pending");
        Host.Services.GetRequiredService<IntegrationTokens>().Unprotect(live.RefreshToken).ShouldBe(second);
    }

    [Test]
    public async Task Connect_WhenOneIsLive_ShouldRevokeAndReplaceIt()
    {
        var first = await ConnectAsync();

        var second = await ConnectAsync();

        _fake.Revocations.ShouldContain(first);
        _fake.Revocations.ShouldNotContain(second);
        var rows = await RowsAsync();
        rows.Count.ShouldBe(2);
        var archived = rows.Single(r => r.ArchivedAt is not null);
        archived.RefreshToken.ShouldBeEmpty();
        var live = rows.Single(r => r.ArchivedAt is null);
        Host.Services.GetRequiredService<IntegrationTokens>().Unprotect(live.RefreshToken).ShouldBe(second);
    }

    // ---- Read

    [Test]
    public async Task Read_ShouldBeOffWhenNoClient()
    {
        _settings.Client = null;
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.Off);
        using var off = await Api.ActingAs(TestUser.Joel).GetAsync($"/api/projects/{SeedIds.Projects.Ship}/demo-settings");
        (await Json(off))["youTube"]!["status"]!.GetValue<string>().ShouldBe("off");

        // Control: with the client back, the same read says what the project has.
        _settings.Client = new YouTubeClientSettings(ClientId, ClientSecret);
        (await ReadAsync()).Status.ShouldBe(YouTubeConnectionStatus.NotConnected);
        await ConnectAsync();
        var pending = await ReadAsync();
        pending.Status.ShouldBe(YouTubeConnectionStatus.Pending);
        pending.ChannelTitle.ShouldBe("Fake channel");
        using var on = await Api.ActingAs(TestUser.Joel).GetAsync($"/api/projects/{SeedIds.Projects.Ship}/demo-settings");
        (await Json(on))["youTube"]!["status"]!.GetValue<string>().ShouldBe("pending");
    }

    // ---- Helpers

    private YouTubeConnectState States() => Host.Services.GetRequiredService<YouTubeConnectState>();

    private async Task<YouTubeProjectDto> ReadAsync()
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IYouTubeConnections>().ReadAsync(SeedIds.Projects.Ship);
    }

    private static async Task<JsonNode> Json(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static string Redirect(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        return response.Headers.Location!.OriginalString;
    }

    private async Task<string> StartStateAsync(TestUser user)
    {
        using var response = await Api.ActingAs(user).PostJsonAsync($"{Project}/start");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var url = (await Json(response))["url"]!.GetValue<string>();
        return QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();
    }

    // The browser's trip back from Google. A null user is a signed-out browser.
    private static HttpRequestMessage Browser(Guid? user, string? state, string? code, string? error)
    {
        var query = new Dictionary<string, string?> { ["state"] = state, ["code"] = code, ["error"] = error }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value);
        var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString("/api/youtube/callback", query));
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        request.Headers.Add(TestAuthentication.UserHeader, user?.ToString() ?? TestAuthentication.AnonymousValue);
        return request;
    }

    private async Task<HttpResponseMessage> CallbackAsync(Guid? user, string? state, string? code = Code, string? error = null)
    {
        using var request = Browser(user, state, code, error);
        return await _browser.SendAsync(request);
    }

    private DateTimeOffset Now() => Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    private async Task CallbackQuietlyAsync(Guid? user, string? state)
    {
        using var response = await CallbackAsync(user, state);
    }

    private async Task DisconnectAsync()
    {
        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/disconnect");
    }

    // Start, then the callback: returns the refresh token Google gave for it.
    private async Task<string> ConnectAsync()
    {
        var state = await StartStateAsync(TestUser.Joel);
        using var response = await CallbackAsync(SeedIds.Users.Joel, state);
        Redirect(response).ShouldBe("/p/SHIP/settings/demos?youtube=pending");
        return _client.Latest.RefreshToken;
    }

    private async Task SetRoleAsync(Guid user, string role)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE project_member SET role = @role WHERE project_id = @project AND user_id = @user", new { role, project = SeedIds.Projects.Ship, user });
    }

    private async Task<string> BaseUrlAsync() => (await ReadSeedBaseUrlAsync()).TrimEnd('/');

    private async Task<string> ReadSeedBaseUrlAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<string>("SELECT base_url FROM instance_settings") ?? string.Empty;
    }

    private async Task SetBaseUrlAsync(string value)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @value", new { value });
    }

    private sealed record ChannelRow(string Status, string ChannelId, string ChannelTitle, string? Visibility, DateTimeOffset? CheckedAt, string? Refusal, Guid ConnectedBy, string RefreshToken, DateTimeOffset? ArchivedAt);

    private async Task<List<ChannelRow>> RowsAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return (await database.QueryAsync<ChannelRow>(
            """
            SELECT status AS Status, channel_id AS ChannelId, channel_title AS ChannelTitle, visibility AS Visibility, checked_at AS CheckedAt,
                   refusal AS Refusal, connected_by AS ConnectedBy, refresh_token AS RefreshToken, archived_at AS ArchivedAt
            FROM project_youtube_channel WHERE project_id = @project AND created_at >= @since ORDER BY created_at
            """,
            new { project = SeedIds.Projects.Ship, since = _since })).ToList();
    }

    private sealed class SettableSettings : IYouTubeSettings
    {
        public YouTubeClientSettings? Client { get; set; }

        public Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Client);
    }

    // The fake, plus what it handed out (so a test can name the tokens) and a switch for a consent that comes back without a refresh token.
    private sealed class TrackingClient(FakeYouTubeClient inner) : IYouTubeClient
    {
        private readonly List<YouTubeTokens> _granted = [];
        private readonly List<YouTubeTokens> _issued = [];

        public bool DropRefreshToken { get; set; }

        public YouTubeTokens Latest
        {
            get { lock (_granted) { return _granted[^1]; } }
        }

        // Everything handed out since the host started, for the test that reads the whole log.
        public IReadOnlyList<YouTubeTokens> Issued
        {
            get { lock (_granted) { return [.. _issued]; } }
        }

        public void Forget()
        {
            lock (_granted)
            {
                _granted.Clear();
            }
        }

        public IReadOnlyList<YouTubeTokens> Granted
        {
            get { lock (_granted) { return [.. _granted]; } }
        }

        public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) => inner.AuthorizationUrl(client, redirectUri, state);

        public async Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken)
        {
            var tokens = await inner.ExchangeCodeAsync(client, code, redirectUri, cancellationToken);
            if (DropRefreshToken)
            {
                tokens = new YouTubeTokens(tokens.AccessToken, string.Empty, tokens.GrantedScopes);
            }

            lock (_granted)
            {
                _granted.Add(tokens);
                _issued.Add(tokens);
            }

            return tokens;
        }

        public Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => inner.RevokeAsync(client, refreshToken, cancellationToken);

        public Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => inner.AccessTokenAsync(client, refreshToken, cancellationToken);

        public Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken) => inner.ChannelAsync(accessToken, cancellationToken);

        public Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken) => inner.UploadAsync(accessToken, upload, content, length, cancellationToken);

        public Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken) => inner.ReadPrivacyAsync(accessToken, videoId, cancellationToken);

        public Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken) => inner.DeleteAsync(accessToken, videoId, cancellationToken);
    }
}

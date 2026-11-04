using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport.Contracts.YouTube;
using ShipLean.TestSupport.YouTube;

namespace ShipLean.WebApi.Tests.Demos;

// YouTubeClientContract on the real client over TestYouTubeServer, plus what only the real client has: the request shapes.
// Nothing here talks to Google, so none of it proves Google answers as the stub does.
[TestFixture]
public class GoogleYouTubeClientContractTests : YouTubeClientContract
{
    private static readonly YouTubeClientSettings Settings = new("client-id", "client-secret");

    private TestYouTubeServer _server = null!;
    private IHost _host = null!;

    [SetUp]
    public async Task StartServer()
    {
        _server = await TestYouTubeServer.StartAsync();
        _host = Host(_server);
    }

    [TearDown]
    public async Task StopServer()
    {
        _host.Dispose();
        await _server.DisposeAsync();
    }

    protected override IYouTubeClient Create() => _host.Services.CreateScope().ServiceProvider.GetRequiredService<IYouTubeClient>();

    protected override void ForceVisibility(IYouTubeClient client, YouTubeVisibility visibility) => _server.ForcedVisibility = visibility;

    protected override void ExhaustQuota(IYouTubeClient client) => _server.QuotaExceeded = true;

    protected override void RevokeAccess(IYouTubeClient client) => _server.Revoked = true;

    protected override void NarrowGrantedScope(IYouTubeClient client) => _server.NarrowScope = true;

    protected override void FailRevoke(IYouTubeClient client) => _server.RevokeFails = true;

    [Test]
    public void AuthorizationUrl_ShouldAskForOfflineConsentAndForceSslScope()
    {
        var url = new Uri(Create().AuthorizationUrl(Settings, "https://x.test/api/youtube/callback", "the state"));

        $"{url.Scheme}://{url.Authority}{url.AbsolutePath}".ShouldBe(_server.Authorization);
        var query = QueryHelpers.ParseQuery(url.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        query["scope"].ShouldBe("https://www.googleapis.com/auth/youtube.force-ssl");
        query["access_type"].ShouldBe("offline");
        query["prompt"].ShouldBe("consent");
        query["response_type"].ShouldBe("code");
        query["client_id"].ShouldBe("client-id");
        query["redirect_uri"].ShouldBe("https://x.test/api/youtube/callback");
        query["state"].ShouldBe("the state");
        query.ShouldNotContainKey("client_secret");
    }

    [Test]
    public async Task Exchange_WhenNoRefreshToken_ShouldFail()
    {
        _server.OmitRefreshToken = true;

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => Create().ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Rejected);
        failure.Message.ShouldContain("refresh token");
        failure.Message.ShouldNotContain("access-");
    }

    [Test]
    public async Task Exchange_ShouldParseTheGrantedScope()
    {
        _server.Scope = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.force-ssl";

        var tokens = await Create().ExchangeCodeAsync(Settings, "the-code", "https://x.test/cb", CancellationToken.None);

        tokens.GrantedScopes.ShouldBe(["https://www.googleapis.com/auth/youtube.upload", "https://www.googleapis.com/auth/youtube.force-ssl"]);
        var call = _server.Calls.Single(c => c.Path == "/token");
        var form = QueryHelpers.ParseQuery(call.Body!).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        form["grant_type"].ShouldBe("authorization_code");
        form["code"].ShouldBe("the-code");
        form["redirect_uri"].ShouldBe("https://x.test/cb");
        form["client_id"].ShouldBe("client-id");
        form["client_secret"].ShouldBe("client-secret");
    }

    [Test]
    public async Task Revoke_ShouldPostTheTokenToTheRevokeEndpoint()
    {
        var client = Create();
        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);

        await client.RevokeAsync(Settings, tokens.RefreshToken, CancellationToken.None);

        var call = _server.Calls.Single(c => c.Path == "/revoke");
        call.Method.ShouldBe("POST");
        call.Headers["Content-Type"].ShouldStartWith("application/x-www-form-urlencoded");
        call.Body.ShouldBe($"token={tokens.RefreshToken}");
        call.Headers.ShouldNotContainKey("Authorization");
    }

    [Test]
    public async Task Upload_ShouldUseResumableAndStreamTheBody()
    {
        const long length = 4 * 1024 * 1024;
        var source = new ProbedStream(length, () => _server.UploadedBytes);

        var id = await Create().UploadAsync(
            "the-access-token", new YouTubeUpload("My title", "0:00 Intro", YouTubeVisibility.Unlisted, "video/mp4"), source, length, CancellationToken.None);

        id.ShouldNotBeNullOrWhiteSpace();
        var start = _server.Calls.Single(c => c.Method == "POST" && c.Path == "/upload/youtube/v3/videos");
        start.Query["uploadType"].ShouldBe("resumable");
        start.Query["part"].ShouldBe("snippet,status");
        start.Headers["Authorization"].ShouldBe("Bearer the-access-token");
        start.Headers["X-Upload-Content-Length"].ShouldBe(length.ToString());
        start.Headers["X-Upload-Content-Type"].ShouldBe("video/mp4");
        var metadata = JsonDocument.Parse(start.Body!).RootElement;
        metadata.GetProperty("snippet").GetProperty("title").GetString().ShouldBe("My title");
        metadata.GetProperty("snippet").GetProperty("description").GetString().ShouldBe("0:00 Intro");
        metadata.GetProperty("status").GetProperty("privacyStatus").GetString().ShouldBe("unlisted");
        metadata.GetProperty("status").GetProperty("selfDeclaredMadeForKids").GetBoolean().ShouldBeFalse();

        var put = _server.Calls.Single(c => c.Method == "PUT");
        put.Headers["Authorization"].ShouldBe("Bearer the-access-token");
        put.Headers["Content-Type"].ShouldBe("video/mp4");
        put.Headers["Content-Length"].ShouldBe(length.ToString());
        put.BodyBytes.ShouldBe(length);
        // ReceivedAtEnd is the assert that guards streaming: the stub's own read counts hold for a buffering client too.
        source.ReceivedAtEnd.ShouldBeGreaterThan(0, "the stub received nothing before the client had read the whole source: the body was buffered");
    }

    [Test]
    public async Task Failure_ShouldCarryNoTokenInItsMessage()
    {
        var client = Create();
        var tokens = await client.ExchangeCodeAsync(Settings, "code", "https://x.test/cb", CancellationToken.None);
        _server.Revoked = true;

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => client.AccessTokenAsync(Settings, tokens.RefreshToken, CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Revoked);
        _server.Calls[^1].Body!.ShouldContain(tokens.RefreshToken, Case.Sensitive);
        failure.Message.ShouldNotContain(tokens.RefreshToken);
        failure.Message.ShouldNotContain("client-secret");
        failure.ToString().ShouldNotContain(tokens.RefreshToken);
    }

    [TestCase("localhost")]
    [TestCase("127.0.0.2")]
    public async Task Upload_WhenResumeUrlIsAnotherHostOnTheSamePort_ShouldRefuse(string host)
    {
        var port = new Uri(_server.BaseUrl).Port;
        _server.SessionLocationOverride = $"http://{host}:{port}/upload/youtube/v3/videos?uploadType=resumable&upload_id=s99";

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => Create().UploadAsync(
            "the-access-token", new YouTubeUpload("T", "D", YouTubeVisibility.Unlisted, "video/mp4"), new MemoryStream([1, 2, 3]), 3, CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Rejected);
        failure.Message.ShouldContain("address ShipLean won't send the video to");
        _server.Calls.ShouldNotContain(c => c.Method == "PUT");
    }

    [Test]
    public async Task Upload_WhenResumeUrlIsAnotherPort_ShouldRefuse()
    {
        await using var elsewhere = await TestYouTubeServer.StartAsync();
        _server.SessionLocationOverride = $"{elsewhere.Upload}/videos?uploadType=resumable&upload_id=s99";

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => Create().UploadAsync(
            "the-access-token", new YouTubeUpload("T", "D", YouTubeVisibility.Unlisted, "video/mp4"), new MemoryStream([1, 2, 3]), 3, CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Rejected);
        failure.Message.ShouldContain("address ShipLean won't send the video to");
        elsewhere.Calls.ShouldBeEmpty();
        _server.Calls.ShouldNotContain(c => c.Method == "PUT");
    }

    [Test]
    public async Task Channel_WhenGoogleGivesNoTitle_ShouldFailInsteadOfShowingTheId()
    {
        _server.OmitChannelTitle = true;

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => Create().ChannelAsync("the-access-token", CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Rejected);
        failure.Message.ShouldContain("no title");
    }

    [Test]
    public async Task Channel_WhenTheAnswerDropsMidBody_ShouldFailAsNetwork()
    {
        _server.DropChannelBody = true;

        var failure = await Should.ThrowAsync<YouTubeFailureException>(() => Create().ChannelAsync("the-access-token", CancellationToken.None));

        failure.Kind.ShouldBe(YouTubeFailureKind.Network);
    }

    // The stub answers as Google does to a request that is not what the docs describe; the client tests above fail on it because
    // the client then gets the refusal. These pin the stub itself, so that a wrong request cannot slip through it.
    [TestCase("GET", "/youtube/v3/channels?part=snippet", true, 400)]
    [TestCase("GET", "/youtube/v3/channels?mine=true", true, 400)]
    [TestCase("GET", "/youtube/v3/channels?part=snippet&mine=true", false, 401)]
    [TestCase("GET", "/youtube/v3/videos?id=v1", true, 400)]
    [TestCase("GET", "/youtube/v3/videos?part=status", true, 400)]
    [TestCase("GET", "/youtube/v3/videos?part=status&id=v1", false, 401)]
    [TestCase("DELETE", "/youtube/v3/videos", true, 400)]
    [TestCase("DELETE", "/youtube/v3/videos?id=v1", false, 401)]
    [TestCase("POST", "/upload/youtube/v3/videos?part=snippet,status", true, 400)]
    [TestCase("POST", "/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status", false, 401)]
    [TestCase("PUT", "/upload/youtube/v3/videos?uploadType=resumable&upload_id=s1", false, 401)]
    public async Task Stub_ShouldRefuseWhatGoogleRefuses(string method, string path, bool withBearer, int expected)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), _server.BaseUrl + path);
        if (withBearer)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "t");
        }

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        using var reply = await http.SendAsync(request);

        ((int)reply.StatusCode).ShouldBe(expected);
    }

    [TestCase("/token", "grant_type=password&client_id=a&client_secret=b", "unsupported_grant_type")]
    [TestCase("/token", "grant_type=authorization_code&client_id=a&client_secret=b&code=c", "invalid_request")]
    [TestCase("/token", "grant_type=authorization_code&client_id=a&code=c&redirect_uri=r", "invalid_request")]
    [TestCase("/token", "grant_type=refresh_token&client_secret=b&refresh_token=r", "invalid_request")]
    [TestCase("/token", "grant_type=refresh_token&client_id=a&client_secret=b", "invalid_request")]
    [TestCase("/revoke", "tok=1", "invalid_request")]
    public async Task Stub_ShouldRefuseAFormGoogleRefuses(string path, string form, string error)
    {
        using var http = new HttpClient();
        using var content = new StringContent(form, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

        using var reply = await http.PostAsync(_server.BaseUrl + path, content);

        ((int)reply.StatusCode).ShouldBe(400);
        JsonDocument.Parse(await reply.Content.ReadAsStringAsync()).RootElement.GetProperty("error").GetString().ShouldBe(error);
    }

    private static IHost Host(TestYouTubeServer server)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(null);
        var services = builder.Services;
        new YouTubeServices().AddServices(services, builder.Configuration);
        services.Configure<YouTubeEndpoints>(endpoints =>
        {
            endpoints.Authorization = server.Authorization;
            endpoints.Token = server.Token;
            endpoints.Revoke = server.Revoke;
            endpoints.Api = server.Api;
            endpoints.Upload = server.Upload;
        });
        return builder.Build();
    }
}

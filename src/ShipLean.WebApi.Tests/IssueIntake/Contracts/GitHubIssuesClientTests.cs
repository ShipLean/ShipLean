using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Contracts;

// The GitHub issues HTTP contract (C3): GitHubIssuesClient against a stub of the GitHub REST API over https on loopback. Each
// request is checked against GitHub's documented shape, and each status against the result the outbox acts on.
[TestFixture]
public class GitHubIssuesClientTests
{
    private const string Token = "ghp_test_token";
    private const string IssuePath = "/repos/acme/storefront/issues/64";
    private const string CommentsPath = IssuePath + "/comments";

    private static readonly IssueRef Issue = new(IssueRef.GitHubHost, "acme/storefront", 64);

    private HttpsStubServer _server = null!;
    private CapturingClients _clients = null!;

    [SetUp]
    public async Task StartTheStub()
    {
        _server = await HttpsStubServer.StartAsync();
        _clients = new CapturingClients();
    }

    [TearDown]
    public async Task StopTheStub() => await _server.DisposeAsync();

    private GitHubIssuesClient Client(string? token = Token, string? apiUrl = null)
        => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(token, apiUrl ?? _server.BaseAddress.AbsoluteUri, null, null, [])));

    // Whole seconds in UTC, as GitHub writes times.
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().AddDays(-1).ToUnixTimeSeconds());

    private static string CommentJson(long id, string login, string body, DateTimeOffset createdAt)
        => $$"""{ "id": {{id}}, "user": { "login": "{{login}}" }, "body": {{JsonSerializer.Serialize(body)}}, "created_at": "{{GitHubTime(createdAt)}}", "author_association": "OWNER" }""";

    private static string GitHubTime(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    [Test]
    public async Task Comment_ShouldPostBodyAndReturnId()
    {
        _server.Respond(CommentsPath, CommentJson(4_294_967_301, "shiplean-bot", "Thanks", Yesterday), 201);

        var result = await Client().CommentAsync(Issue, "Thanks for the report.\n\n<!-- shiplean:outbox:x -->");

        result.ShouldBe(new GitHubWriteResult.Ok(4_294_967_301));
        var sent = _clients.Sent.ShouldHaveSingleItem();
        sent.Method.ShouldBe("POST");
        sent.PathAndQuery.ShouldBe(CommentsPath);
        sent.Authorization.ShouldBe($"Bearer {Token}");
        sent.Accept.ShouldBe("application/vnd.github+json");
        JsonDocument.Parse(sent.Body!).RootElement.GetProperty("body").GetString().ShouldBe("Thanks for the report.\n\n<!-- shiplean:outbox:x -->");
    }

    [Test]
    public async Task Comment_WhenForbidden_ShouldFail()
    {
        _server.Respond(CommentsPath, """{ "message": "Resource not accessible by integration" }""", 403);

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Failed("403"));
    }

    [TestCase(401)]
    [TestCase(404)]
    [TestCase(410)]
    [TestCase(422)]
    public async Task Comment_WhenRefused_ShouldFail(int status)
    {
        _server.Respond(CommentsPath, """{ "message": "no" }""", status);

        (await Client().CommentAsync(Issue, "Hi")).ShouldBeOfType<GitHubWriteResult.Failed>();
    }

    [Test]
    public async Task Comment_WhenTooManyRequests_ShouldRetry()
    {
        _server.Respond(CommentsPath, """{ "message": "rate limited" }""", 429);

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Retry("429"));
    }

    [Test]
    public async Task Comment_WhenForbiddenByRateLimit_ShouldRetry()
    {
        _server.Respond(CommentsPath, """{ "message": "API rate limit exceeded" }""", 403);
        _clients.AddResponseHeader("x-ratelimit-remaining", "0");

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Retry("403"));
    }

    [Test]
    public async Task Comment_WhenServerError_ShouldBeAmbiguous()
    {
        _server.Respond(CommentsPath, "{}", 502);

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Ambiguous("502"));
    }

    [Test]
    public async Task Comment_WhenTimeout_ShouldBeAmbiguous()
    {
        _server.Respond(CommentsPath, CommentJson(1, "shiplean-bot", "Hi", Yesterday), 201);
        _clients.HangAfterSending = true;

        var result = await Client().CommentAsync(Issue, "Hi");

        result.ShouldBe(new GitHubWriteResult.Ambiguous("timeout"));
        _server.Requests.ShouldBe([$"POST {CommentsPath}"]);
    }

    [Test]
    public async Task Comment_WhenConnectionRefused_ShouldRetry()
    {
        // A loopback port nothing listens on: the connect is refused, so no request bytes can have gone out.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        (await Client(apiUrl: $"https://127.0.0.1:{port}/").CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Retry("ConnectionError"));
    }

    [TestCase(HttpRequestError.ConnectionError)]
    [TestCase(HttpRequestError.SecureConnectionError)]
    [TestCase(HttpRequestError.ResponseEnded)]
    public async Task Comment_WhenTransportFailsAfterSending_ShouldBeAmbiguous(HttpRequestError error)
    {
        _server.Respond(CommentsPath, CommentJson(1, "shiplean-bot", "Hi", Yesterday), 201);
        _clients.FailAfterSending = () => new HttpRequestException(error, "Connection reset by peer", new IOException("reset", new SocketException((int)SocketError.ConnectionReset)));

        var result = await Client().CommentAsync(Issue, "Hi");

        result.ShouldBe(new GitHubWriteResult.Ambiguous(error.ToString()));
        _server.Requests.ShouldBe([$"POST {CommentsPath}"]);
    }

    [Test]
    public async Task Close_WhenTransportFailsAfterSending_ShouldRetry()
    {
        _server.Respond(IssuePath, """{ "number": 64, "state": "closed" }""");
        _clients.FailAfterSending = () => new HttpRequestException(HttpRequestError.ResponseEnded, "ended");

        (await Client().CloseAsync(Issue, CloseReason.Completed)).ShouldBe(new GitHubWriteResult.Retry("ResponseEnded"));
    }

    [Test]
    public async Task Comment_WhenReplyBodyBreaksOff_ShouldBeAmbiguous()
    {
        _server.Respond(CommentsPath, CommentJson(1, "shiplean-bot", "Hi", Yesterday), 201);
        _clients.ReplaceBody = () => ScriptedStream.Breaking("""{ "id": 7, "body": "H""");

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Ambiguous("unreadableReply"));
    }

    [Test]
    public async Task Comment_WhenCancelledWhileReadingReply_ShouldThrow()
    {
        using var cancellation = new CancellationTokenSource();
        _server.Respond(CommentsPath, CommentJson(1, "shiplean-bot", "Hi", Yesterday), 201);
        _clients.ReplaceBody = () => new ScriptedStream(1, _ => (byte)'{', () =>
        {
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
        });

        await Should.ThrowAsync<OperationCanceledException>(() => Client().CommentAsync(Issue, "Hi", cancellation.Token));
    }

    [Test]
    public async Task Comment_WhenReplyHasNoId_ShouldBeAmbiguous()
    {
        _server.Respond(CommentsPath, """{ "body": "Hi", "user": { "login": "shiplean-bot" } }""", 201);

        (await Client().CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Ambiguous("unreadableReply"));
    }

    [Test]
    public async Task Comment_WhenNoToken_ShouldFailWithoutCalling()
    {
        (await Client(token: null).CommentAsync(Issue, "Hi")).ShouldBe(new GitHubWriteResult.Failed("noToken"));

        _clients.Sent.ShouldBeEmpty();
    }

    [TestCase(CloseReason.Completed, "completed")]
    [TestCase(CloseReason.NotPlanned, "not_planned")]
    public async Task Close_ShouldSendStateReason(CloseReason reason, string stateReason)
    {
        _server.Respond(IssuePath, """{ "number": 64, "state": "closed" }""");

        var result = await Client().CloseAsync(Issue, reason);

        result.ShouldBe(new GitHubWriteResult.Ok(null));
        var sent = _clients.Sent.ShouldHaveSingleItem();
        sent.Method.ShouldBe("PATCH");
        sent.PathAndQuery.ShouldBe(IssuePath);
        var body = JsonDocument.Parse(sent.Body!).RootElement;
        body.GetProperty("state").GetString().ShouldBe("closed");
        body.GetProperty("state_reason").GetString().ShouldBe(stateReason);
    }

    [Test]
    public async Task Close_WhenServerError_ShouldRetry()
    {
        _server.Respond(IssuePath, "{}", 503);

        (await Client().CloseAsync(Issue, CloseReason.Completed)).ShouldBe(new GitHubWriteResult.Retry("503"));
    }

    [Test]
    public async Task Reopen_ShouldSendOpenAndReopened()
    {
        _server.Respond(IssuePath, """{ "number": 64, "state": "open" }""");

        (await Client().ReopenAsync(Issue)).ShouldBe(new GitHubWriteResult.Ok(null));

        var body = JsonDocument.Parse(_clients.Sent.ShouldHaveSingleItem().Body!).RootElement;
        body.GetProperty("state").GetString().ShouldBe("open");
        body.GetProperty("state_reason").GetString().ShouldBe("reopened");
    }

    [Test]
    public async Task ListRecent_ShouldPassSince()
    {
        _server.Respond(
            CommentsPath,
            $"[{CommentJson(11, "reporter", "LGTM", Yesterday.AddSeconds(5))}, {CommentJson(12, "shiplean-bot", "Done <!-- shiplean:outbox:a -->", Yesterday.AddMinutes(1))}]");

        // Sent in UTC whatever the offset given.
        var result = await Client().ListRecentCommentsAsync(Issue, Yesterday.ToOffset(TimeSpan.FromHours(7)));

        var sent = _clients.Sent.ShouldHaveSingleItem();
        sent.Method.ShouldBe("GET");
        sent.PathAndQuery.ShouldBe($"{CommentsPath}?since={Uri.EscapeDataString(GitHubTime(Yesterday))}&per_page=100&page=1");
        result.ShouldBeOfType<GitHubCommentList.Listed>().ShouldSatisfyAllConditions(
            listed => listed.Complete.ShouldBeTrue(),
            listed => listed.Comments.ShouldBe([
                new GitHubComment(11, "reporter", "LGTM", Yesterday.AddSeconds(5)),
                new GitHubComment(12, "shiplean-bot", "Done <!-- shiplean:outbox:a -->", Yesterday.AddMinutes(1)),
            ]));
    }

    [Test]
    public async Task ListRecent_WhenPagesStayFull_ShouldStopAtFiveHundred()
    {
        // The stub answers every page with the same 100 comments, so the client has to stop on its own.
        _server.Respond(CommentsPath, $"[{string.Join(',', Enumerable.Range(1, 100).Select(i => CommentJson(i, "someone", "x", Yesterday)))}]");

        var result = await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch);

        var listed = result.ShouldBeOfType<GitHubCommentList.Listed>();
        listed.Comments.Count.ShouldBe(IGitHubIssues.MaxListedComments);
        listed.Complete.ShouldBeFalse();
        _clients.Sent.Select(s => s.PathAndQuery[^6..]).ShouldBe(["page=1", "page=2", "page=3", "page=4", "page=5"]);
    }

    [Test]
    public async Task ListRecent_WhenNotFound_ShouldFail()
    {
        var result = await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch);

        result.ShouldBe(new GitHubCommentList.Failed("404"));
    }

    [Test]
    public async Task ListRecent_WhenPageIsNotAList_ShouldRetry()
    {
        _server.Respond(CommentsPath, """{ "message": "not a list" }""");

        (await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch)).ShouldBe(new GitHubCommentList.Retry("unreadableReply"));
    }

    [Test]
    public async Task ListRecent_WhenPageIsOverSixteenMegabytes_ShouldRetry()
    {
        // A valid empty list padded with whitespace past the 16 MB cap.
        const long length = (16 * 1024 * 1024) + 2;
        _server.Respond(CommentsPath, "[]");
        _clients.ReplaceBody = () => new ScriptedStream(length, i => i == 0 ? (byte)'[' : SpaceOrClose(i, length));

        (await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch)).ShouldBe(new GitHubCommentList.Retry("unreadableReply"));
    }

    private static byte SpaceOrClose(long i, long length) => i == length - 1 ? (byte)']' : (byte)' ';

    [Test]
    public async Task ListRecent_WhenPageBodyBreaksOff_ShouldRetry()
    {
        _server.Respond(CommentsPath, "[]");
        _clients.ReplaceBody = () => ScriptedStream.Breaking("""[{ "id": 1""");

        (await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch)).ShouldBe(new GitHubCommentList.Retry("unreadableReply"));
    }

    [Test]
    public async Task SelfLogin_ShouldAskGitHubOnceAnHour()
    {
        _server.Respond("/user", """{ "login": "shiplean-bot", "id": 7 }""");
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        var identity = new GitHubIdentity(new ClientScopes(() => Client()), clock);

        (await identity.SelfLoginAsync()).ShouldBe("shiplean-bot");
        clock.Advance(TimeSpan.FromMinutes(59));
        (await identity.SelfLoginAsync()).ShouldBe("shiplean-bot");
        _server.Requests.ShouldBe(["GET /user"]);

        clock.Advance(TimeSpan.FromMinutes(1));
        await identity.SelfLoginAsync();
        _server.Requests.ShouldBe(["GET /user", "GET /user"]);
    }

    // Scopes that each hold one new client, standing in for the host's scopes (the client is scoped there).
    private sealed class ClientScopes(Func<GitHubIssuesClient> create) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(create());

        private sealed class Scope(GitHubIssuesClient client) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) => serviceType == typeof(GitHubIssuesClient) ? client : null;

            public void Dispose()
            {
            }
        }
    }

    // One request as it left the client, read before it was sent (the client disposes it afterwards).
    private sealed record SentRequest(string Method, string PathAndQuery, string? Authorization, string? Accept, string? Body);

    // An IHttpClientFactory whose clients call the stub (accepting its self-signed certificate) and keep each request's shape.
    // HangAfterSending makes a client time out after the stub has received the request; ResponseHeaders are added to answers.
    // FailAfterSending throws its exception once the stub has the request; ReplaceBody swaps the answer's body for a stream.
    private sealed class CapturingClients : IHttpClientFactory
    {
        private readonly ConcurrentQueue<SentRequest> _sent = new();

        public IReadOnlyList<SentRequest> Sent => [.. _sent];

        public bool HangAfterSending { get; set; }

        public Func<Exception>? FailAfterSending { get; set; }

        public Func<Stream>? ReplaceBody { get; set; }

        private readonly Dictionary<string, string> _responseHeaders = [];

        public void AddResponseHeader(string name, string value) => _responseHeaders[name] = value;

        public HttpClient CreateClient(string name)
        {
            var inner = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            return new HttpClient(new Capture(this, inner)) { Timeout = HangAfterSending ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(15) };
        }

        private sealed class Capture(CapturingClients owner, HttpMessageHandler inner) : DelegatingHandler(inner)
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                owner._sent.Enqueue(new SentRequest(
                    request.Method.Method,
                    request.RequestUri!.PathAndQuery,
                    request.Headers.Authorization?.ToString(),
                    string.Join(", ", request.Headers.Accept),
                    body));
                var response = await base.SendAsync(request, cancellationToken);
                foreach (var (header, value) in owner._responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(header, value);
                }

                if (owner.FailAfterSending is { } fail)
                {
                    response.Dispose();
                    throw fail();
                }

                if (owner.ReplaceBody is { } replace)
                {
                    response.Content = new StreamContent(replace());
                }

                if (owner.HangAfterSending)
                {
                    response.Dispose();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return response;
            }
        }
    }

    // A read-only body of length bytes, byteAt(i) each; at the end it calls atEnd (which may throw), else it ends.
    private sealed class ScriptedStream(long length, Func<long, byte> byteAt, Action? atEnd = null) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        // The text, then the connection drops as HttpClient reports it.
        public static ScriptedStream Breaking(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return new ScriptedStream(bytes.Length, i => bytes[i], () => throw new HttpIOException(HttpRequestError.ResponseEnded));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position == length)
            {
                atEnd?.Invoke();
                return 0;
            }

            var read = (int)Math.Min(count, length - _position);
            for (var i = 0; i < read; i++)
            {
                buffer[offset + i] = byteAt(_position++);
            }

            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

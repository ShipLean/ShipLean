using System.Collections.Concurrent;
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

    private GitHubIssuesClient Client(string? token = Token)
        => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(token, _server.BaseAddress.AbsoluteUri, null, null, [])));

    private static string CommentJson(long id, string login, string body, string createdAt)
        => $$"""{ "id": {{id}}, "user": { "login": "{{login}}" }, "body": {{JsonSerializer.Serialize(body)}}, "created_at": "{{createdAt}}", "author_association": "OWNER" }""";

    [Test]
    public async Task Comment_ShouldPostBodyAndReturnId()
    {
        _server.Respond(CommentsPath, CommentJson(4_294_967_301, "shiplean-bot", "Thanks", "2026-11-08T06:00:00Z"), 201);

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
        _clients.ResponseHeaders["x-ratelimit-remaining"] = "0";

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
        _server.Respond(CommentsPath, CommentJson(1, "shiplean-bot", "Hi", "2026-11-08T06:00:00Z"), 201);
        _clients.HangAfterSending = true;

        var result = await Client().CommentAsync(Issue, "Hi");

        result.ShouldBe(new GitHubWriteResult.Ambiguous("timeout"));
        _server.Requests.ShouldBe([$"POST {CommentsPath}"]);
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
            $"[{CommentJson(11, "reporter", "LGTM", "2026-11-07T10:00:05Z")}, {CommentJson(12, "shiplean-bot", "Done <!-- shiplean:outbox:a -->", "2026-11-07T10:01:00Z")}]");

        var result = await Client().ListRecentCommentsAsync(Issue, new DateTimeOffset(2026, 11, 7, 17, 0, 0, TimeSpan.FromHours(7)));

        var sent = _clients.Sent.ShouldHaveSingleItem();
        sent.Method.ShouldBe("GET");
        sent.PathAndQuery.ShouldBe($"{CommentsPath}?since=2026-11-07T10%3A00%3A00Z&per_page=100&page=1");
        result.ShouldBeOfType<GitHubCommentList.Listed>().ShouldSatisfyAllConditions(
            listed => listed.Complete.ShouldBeTrue(),
            listed => listed.Comments.ShouldBe([
                new GitHubComment(11, "reporter", "LGTM", new DateTimeOffset(2026, 11, 7, 10, 0, 5, TimeSpan.Zero)),
                new GitHubComment(12, "shiplean-bot", "Done <!-- shiplean:outbox:a -->", new DateTimeOffset(2026, 11, 7, 10, 1, 0, TimeSpan.Zero)),
            ]));
    }

    [Test]
    public async Task ListRecent_WhenPagesStayFull_ShouldStopAtFiveHundred()
    {
        // The stub answers every page with the same 100 comments, so the client has to stop on its own.
        _server.Respond(CommentsPath, $"[{string.Join(',', Enumerable.Range(1, 100).Select(i => CommentJson(i, "someone", "x", "2026-11-07T10:00:00Z")))}]");

        var result = await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch);

        var listed = result.ShouldBeOfType<GitHubCommentList.Listed>();
        listed.Comments.Count.ShouldBe(IGitHubIssues.MaxListedComments);
        listed.Complete.ShouldBeFalse();
        _clients.Sent.Select(s => s.PathAndQuery[^6..]).ShouldBe(["page=1", "page=2", "page=3", "page=4", "page=5"]);
    }

    [Test]
    public async Task ListRecent_WhenNotFound_ShouldFail()
        => (await Client().ListRecentCommentsAsync(Issue, DateTimeOffset.UnixEpoch)).ShouldBe(new GitHubCommentList.Failed("404"));

    [Test]
    public async Task SelfLogin_ShouldAskGitHubOnceAnHour()
    {
        _server.Respond("/user", """{ "login": "shiplean-bot", "id": 7 }""");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 6, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection()
            .AddSingleton<IHttpClientFactory>(_clients)
            .AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings(new IntegrationSettingsDto(Token, _server.BaseAddress.AbsoluteUri, null, null, [])))
            .AddScoped<GitHubIssuesClient>()
            .BuildServiceProvider();
        var identity = new GitHubIdentity(services.GetRequiredService<IServiceScopeFactory>(), clock);

        (await identity.SelfLoginAsync()).ShouldBe("shiplean-bot");
        clock.Advance(TimeSpan.FromMinutes(59));
        (await identity.SelfLoginAsync()).ShouldBe("shiplean-bot");
        _server.Requests.ShouldBe(["GET /user"]);

        clock.Advance(TimeSpan.FromMinutes(1));
        await identity.SelfLoginAsync();
        _server.Requests.ShouldBe(["GET /user", "GET /user"]);
    }

    // One request as it left the client, read before it was sent (the client disposes it afterwards).
    private sealed record SentRequest(string Method, string PathAndQuery, string? Authorization, string? Accept, string? Body);

    // An IHttpClientFactory whose clients call the stub (accepting its self-signed certificate) and keep each request's shape.
    // HangAfterSending makes a client time out after the stub has received the request; ResponseHeaders are added to answers.
    private sealed class CapturingClients : IHttpClientFactory
    {
        private readonly ConcurrentQueue<SentRequest> _sent = new();

        public IReadOnlyList<SentRequest> Sent => [.. _sent];

        public bool HangAfterSending { get; set; }

        public Dictionary<string, string> ResponseHeaders { get; } = [];

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
                foreach (var (header, value) in owner.ResponseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(header, value);
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
}

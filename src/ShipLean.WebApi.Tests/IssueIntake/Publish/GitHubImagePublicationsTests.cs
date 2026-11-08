using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Application.Features.IssueIntake.Publish;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Publish;

// D14: GitHubImagePublications through the real GitHubIssuesClient against a fake GitHub REST API (an in-process handler; the
// loopback HttpsStubServer's fallback route skips paths whose last segment has a dot, as compare/{fix}...{head} does). It answers
// by path, so each test sets the commit, the docker-publish.yml runs and the compare answers it needs; any other path is a 404.
[TestFixture]
public class GitHubImagePublicationsTests
{
    private const string Repo = "/repos/acme/storefront";
    private const string RunsPath = Repo + "/actions/workflows/docker-publish.yml/runs";
    private const string Fix = "a1b2c3d4e5f60718293a4b5c6d7e8f9011223344";
    private const string Later = "b1b2c3d4e5f60718293a4b5c6d7e8f9011223344";
    private const string Divergent = "c1b2c3d4e5f60718293a4b5c6d7e8f9011223344";

    private static readonly IssueRef Repository = new(IssueRef.GitHubHost, "acme/storefront", 64);

    // Whole seconds in UTC, as GitHub writes times.
    private static readonly DateTimeOffset MergedAt = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().AddDays(-1).ToUnixTimeSeconds());

    private FakeGitHub _clients = null!;
    private FakeTimeProvider _clock = null!;
    private ClientScopes _scopes = null!;

    [SetUp]
    public void StartTheFake()
    {
        _clients = new FakeGitHub();
        _clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        _scopes = new ClientScopes(new GitHubIssuesClient(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto("ghp_test_token", "https://api.github.test/", null, null, []))));
        _clients.Respond($"{Repo}/commits/{Fix}", $$"""{ "sha": "{{Fix}}", "commit": { "committer": { "date": "{{GitHubTime(MergedAt)}}" } } }""");
    }

    private GitHubImagePublications Publications() => new(_scopes, _clock);

    private static string GitHubTime(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string RunUrl(long id) => $"https://github.com/acme/storefront/actions/runs/{id}";

    private static string Run(long id, string sha, string status, string? conclusion, int minutesAfterMerge)
        => $$"""
             { "id": {{id}}, "head_sha": "{{sha}}", "status": "{{status}}", "conclusion": {{(conclusion is null ? "null" : $"\"{conclusion}\"")}},
               "html_url": "{{RunUrl(id)}}", "created_at": "{{GitHubTime(MergedAt.AddMinutes(minutesAfterMerge))}}",
               "updated_at": "{{GitHubTime(MergedAt.AddMinutes(minutesAfterMerge + 6))}}" }
             """;

    // Newest first, as GitHub lists them.
    private void Runs(params string[] runs)
        => _clients.Respond(RunsPath, $$"""{ "total_count": {{runs.Length}}, "workflow_runs": [{{string.Join(",", runs.Reverse())}}] }""");

    private void Compare(string head, string status)
        => _clients.Respond($"{Repo}/compare/{Fix}...{head}", $$"""{ "status": "{{status}}", "ahead_by": 1, "behind_by": 0, "commits": [], "files": [] }""");

    [Test]
    public async Task Find_WhenFixRunSucceeded_ShouldReturnPublished()
    {
        Runs(Run(7, Fix, "completed", "success", 1));

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(7), RunUrl(7), Fix));
        _clients.Sent.Single(p => p.StartsWith(RunsPath, StringComparison.Ordinal))
            .ShouldBe($"{RunsPath}?branch=main&event=push&exclude_pull_requests=true&created=%3E%3D{GitHubTime(MergedAt.AddMinutes(-10)).Replace(":", "%3A", StringComparison.Ordinal)}&per_page=100&page=1");
    }

    [Test]
    public async Task Find_WhenFixRunFailedAndLaterRunContainsFix_ShouldReturnLater()
    {
        Runs(Run(7, Fix, "completed", "failure", 1), Run(8, Later, "completed", "success", 20));
        Compare(Later, "ahead");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(26), RunUrl(8), Later));
    }

    [Test]
    public async Task Find_WhenLaterRunIsOnDivergentCommit_ShouldNotCount()
    {
        Runs(Run(7, Fix, "completed", "failure", 1), Run(8, Divergent, "completed", "success", 10), Run(9, Later, "completed", "success", 20));
        Compare(Divergent, "diverged");
        Compare(Later, "ahead");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(26), RunUrl(9), Later));
    }

    [Test]
    public async Task Find_WhenFixRunFailedAndNothingLaterContainsIt_ShouldReturnFailed()
    {
        Runs(Run(7, Fix, "completed", "failure", 1), Run(8, Divergent, "completed", "success", 10));
        Compare(Divergent, "behind");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Failed(RunUrl(7)));
    }

    [Test]
    public async Task Find_WhenInProgress_ShouldReturnPending()
    {
        Runs(Run(7, Fix, "in_progress", null, 1));

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Pending());
    }

    [Test]
    public async Task Find_WhenNoWorkflow_ShouldReturnNotApplicable()
    {
        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.NotApplicable());
    }

    [Test]
    public async Task Find_WhenCompareUnanswered_ShouldReturnUnknown()
    {
        Runs(Run(8, Divergent, "completed", "success", 10), Run(9, Later, "completed", "success", 20));
        Compare(Later, "ahead");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Unknown());
    }

    [Test]
    public async Task Find_WhenPublished_ShouldKeepAnswerForADay()
    {
        Runs(Run(7, Fix, "completed", "success", 1));
        var publications = Publications();
        await publications.FindAsync(Repository, Fix);
        var asked = _clients.Sent.Count;

        await publications.FindAsync(Repository, Fix);
        _clients.Sent.Count.ShouldBe(asked);

        _clock.Advance(GitHubImagePublications.CacheFor);
        await publications.FindAsync(Repository, Fix);
        _clients.Sent.Count.ShouldBeGreaterThan(asked);
    }

    [Test]
    public async Task Find_WhenPending_ShouldAskAgain()
    {
        Runs(Run(7, Fix, "queued", null, 1));
        var publications = Publications();
        await publications.FindAsync(Repository, Fix);
        var asked = _clients.Sent.Count;

        await publications.FindAsync(Repository, Fix);

        _clients.Sent.Count.ShouldBeGreaterThan(asked);
    }

    // Scopes whose provider hands out the one client (GitHubImagePublications resolves it per lookup).
    private sealed class ClientScopes(GitHubIssuesClient client) : IServiceScopeFactory, IServiceProvider
    {
        public IServiceScope CreateScope() => new Scope(this);

        public object? GetService(Type serviceType) => serviceType == typeof(GitHubIssuesClient) ? client : null;

        private sealed class Scope(IServiceProvider provider) : IServiceScope
        {
            public IServiceProvider ServiceProvider => provider;

            public void Dispose()
            {
            }
        }
    }

    // An IHttpClientFactory whose clients answer from Respond by path (404 for any other) and keep each request's path and query.
    private sealed class FakeGitHub : IHttpClientFactory
    {
        private readonly ConcurrentQueue<string> _sent = new();
        private readonly ConcurrentDictionary<string, string> _answers = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Sent => [.. _sent];

        public void Respond(string path, string body) => _answers[path] = body;

        public HttpClient CreateClient(string name) => new(new Answer(this));

        private sealed class Answer(FakeGitHub owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner._sent.Enqueue(request.RequestUri!.PathAndQuery);
                var response = owner._answers.TryGetValue(request.RequestUri.AbsolutePath, out var body)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{ "message": "Not Found" }""", Encoding.UTF8, "application/json") };
                return Task.FromResult(response);
            }
        }
    }
}

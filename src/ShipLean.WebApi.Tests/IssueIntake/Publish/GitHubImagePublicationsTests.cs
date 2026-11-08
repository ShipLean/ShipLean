using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
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

    private static TestRun Run(long id, string sha, string status, string? conclusion, int minutesAfterMerge, int minutesToFinish = 6)
        => new(MergedAt.AddMinutes(minutesAfterMerge), $$"""
             { "id": {{id}}, "head_sha": "{{sha}}", "status": "{{status}}", "conclusion": {{(conclusion is null ? "null" : $"\"{conclusion}\"")}},
               "html_url": "{{RunUrl(id)}}", "created_at": "{{GitHubTime(MergedAt.AddMinutes(minutesAfterMerge))}}",
               "updated_at": "{{GitHubTime(MergedAt.AddMinutes(minutesAfterMerge + minutesToFinish))}}" }
             """);

    // Newest first, as GitHub lists them, filtered by the created>= bound and cut into per_page pages like GitHub does.
    private void Runs(params TestRun[] runs)
        => _clients.RespondWith(RunsPath, uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            var since = DateTimeOffset.Parse(query["created"]![2..], CultureInfo.InvariantCulture);
            var page = runs.Where(r => r.Created >= since).OrderByDescending(r => r.Created).Skip((int.Parse(query["page"]!, CultureInfo.InvariantCulture) - 1) * 100).Take(100);
            return $$"""{ "total_count": {{runs.Length}}, "workflow_runs": [{{string.Join(",", page.Select(r => r.Json))}}] }""";
        });

    private static string Sha(int n) => n.ToString("x", CultureInfo.InvariantCulture).PadLeft(40, 'd');

    private void Compare(string head, string status)
        => _clients.Respond($"{Repo}/compare/{Fix}...{head}", $$"""{ "status": "{{status}}", "ahead_by": 1, "behind_by": 0, "commits": [], "files": [] }""");

    [Test]
    public async Task Find_WhenFixRunSucceeded_ShouldReturnPublished()
    {
        Runs(Run(7, Fix, "completed", "success", 1));

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(7), RunUrl(7), Fix));
        _clients.Sent.Single(p => p.StartsWith(RunsPath, StringComparison.Ordinal))
            .ShouldBe($"{RunsPath}?branch=main&event=push&exclude_pull_requests=true&created=%3E%3D{GitHubTime(MergedAt.AddHours(-1)).Replace(":", "%3A", StringComparison.Ordinal)}&per_page=100&page=1");
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
    public async Task Find_WhenCommitDateIsAfterAnEarlierContainingRun_ShouldFindThatRun()
    {
        Runs(Run(7, Fix, "completed", "success", -30), Run(8, Later, "completed", "success", 20));
        Compare(Later, "ahead");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(-24), RunUrl(7), Fix));
    }

    [Test]
    public async Task Find_WhenRunListIsTruncated_ShouldReturnUnknown()
    {
        Runs([.. Enumerable.Range(1, 1000).Select(i => Run(i, Sha(i), "completed", "failure", 1 + (i / 100))), Run(2000, Fix, "completed", "success", 30)]);

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Unknown());
    }

    [Test]
    public async Task Find_WhenMoreThanTheLimitIsUnchecked_ShouldReturnUnknown()
    {
        var runs = new List<TestRun> { Run(1, Fix, "completed", "failure", 1) };
        foreach (var i in Enumerable.Range(10, GitHubImagePublications.MaxChecked + 1))
        {
            runs.Add(Run(i, Sha(i), "completed", "success", i));
            Compare(Sha(i), "diverged");
        }

        Runs([.. runs]);

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Unknown());
        _clients.Sent.Count(p => p.Contains("/compare/", StringComparison.Ordinal)).ShouldBe(GitHubImagePublications.MaxChecked);
    }

    [Test]
    public async Task Find_WhenALaterCreatedRunFinishedFirst_ShouldReportIt()
    {
        Runs(Run(7, Later, "completed", "success", 1, 40), Run(8, Divergent, "completed", "success", 5, 3));
        Compare(Later, "ahead");
        Compare(Divergent, "ahead");

        var found = await Publications().FindAsync(Repository, Fix);

        found.ShouldBe(new ImagePublication.Published(MergedAt.AddMinutes(8), RunUrl(8), Divergent));
    }

    [Test]
    public async Task Find_WhenShaIsAbbreviatedAndRunIsPending_ShouldReturnPending()
    {
        _clients.Respond($"{Repo}/commits/{Fix[..7]}", _clients.Answer($"{Repo}/commits/{Fix}"));
        Runs(Run(7, Fix, "in_progress", null, 1));

        var found = await Publications().FindAsync(Repository, Fix[..7]);

        found.ShouldBe(new ImagePublication.Pending());
    }

    [Test]
    public async Task Find_WhenShaIsAbbreviatedAndRunFailed_ShouldReturnFailed()
    {
        _clients.Respond($"{Repo}/commits/{Fix[..7]}", _clients.Answer($"{Repo}/commits/{Fix}"));
        Runs(Run(7, Fix, "completed", "failure", 1));

        var found = await Publications().FindAsync(Repository, Fix[..7]);

        found.ShouldBe(new ImagePublication.Failed(RunUrl(7)));
    }

    [Test]
    public async Task Find_WhenPublished_ShouldExpireAfter24Hours()
    {
        Runs(Run(7, Fix, "completed", "success", 1));

        (await KeptFor(GitHubImagePublications.CacheFor)).ShouldBe((true, true));
    }

    [Test]
    public async Task Find_WhenNotApplicable_ShouldExpireAfterAnHour()
    {
        (await KeptFor(TimeSpan.FromHours(1))).ShouldBe((true, true));
    }

    [Test]
    public async Task Find_WhenPending_ShouldExpireAfterTwoMinutes()
    {
        Runs(Run(7, Fix, "queued", null, 1));

        (await KeptFor(TimeSpan.FromMinutes(2))).ShouldBe((true, true));
    }

    [Test]
    public async Task Find_WhenFailed_ShouldExpireAfterTwoMinutes()
    {
        Runs(Run(7, Fix, "completed", "failure", 1));

        (await KeptFor(TimeSpan.FromMinutes(2))).ShouldBe((true, true));
    }

    [Test]
    public async Task Find_WhenUnknown_ShouldExpireAfterTwoMinutes()
    {
        Runs(Run(8, Later, "completed", "success", 10));

        (await KeptFor(TimeSpan.FromMinutes(2))).ShouldBe((true, true));
    }

    [Test]
    public async Task Find_WhenLookedUpConcurrently_ShouldMakeOneRequest()
    {
        Runs(Run(7, Fix, "completed", "success", 1));
        var gate = new TaskCompletionSource();
        _clients.Gate = gate.Task;
        var publications = Publications();

        var lookups = Enumerable.Range(0, 5).Select(_ => publications.FindAsync(Repository, Fix)).ToList();
        gate.SetResult();
        await Task.WhenAll(lookups);

        _clients.Sent.Count(p => p.Contains("/commits/", StringComparison.Ordinal)).ShouldBe(1);
        _clients.Sent.Count(p => p.StartsWith(RunsPath, StringComparison.Ordinal)).ShouldBe(1);
    }

    // Whether the answer was kept until `lifetime` had almost passed, and asked for again once it had.
    private async Task<(bool Kept, bool AskedAgain)> KeptFor(TimeSpan lifetime)
    {
        var publications = Publications();
        var first = await publications.FindAsync(Repository, Fix);
        var asked = _clients.Sent.Count;

        _clock.Advance(lifetime - TimeSpan.FromSeconds(1));
        var again = await publications.FindAsync(Repository, Fix);
        var kept = again.Equals(first) && _clients.Sent.Count == asked;

        _clock.Advance(TimeSpan.FromSeconds(1));
        await publications.FindAsync(Repository, Fix);
        return (kept, _clients.Sent.Count > asked);
    }

    private sealed record TestRun(DateTimeOffset Created, string Json);

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

        private readonly ConcurrentDictionary<string, Func<Uri, string>> _dynamic = new(StringComparer.Ordinal);

        // Awaited before every answer; a test holds it to keep lookups in flight.
        public Task Gate { get; set; } = Task.CompletedTask;

        public void Respond(string path, string body) => _answers[path] = body;

        public void RespondWith(string path, Func<Uri, string> body) => _dynamic[path] = body;

        public string Answer(string path) => _answers[path];

        public HttpClient CreateClient(string name) => new(new Answerer(this));

        private sealed class Answerer(FakeGitHub owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner._sent.Enqueue(request.RequestUri!.PathAndQuery);
                await owner.Gate;
                var path = request.RequestUri.AbsolutePath;
                var body = owner._dynamic.TryGetValue(path, out var make) ? make(request.RequestUri) : owner._answers.GetValueOrDefault(path);
                return body is not null
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{ "message": "Not Found" }""", Encoding.UTF8, "application/json") };
            }
        }
    }
}

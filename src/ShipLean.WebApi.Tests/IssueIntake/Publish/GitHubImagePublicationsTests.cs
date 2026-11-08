using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Application.Features.IssueIntake.Publish;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.IssueIntake.Publish;

// D14: GitHubImagePublications against a stub of the GitHub REST API over https on loopback. The stub answers by path, so each
// test sets the commit, the docker-publish.yml runs and the compare answers it needs.
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

    private HttpsStubServer _server = null!;
    private CapturingClients _clients = null!;
    private FakeTimeProvider _clock = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public async Task StartTheStub()
    {
        _server = await HttpsStubServer.StartAsync();
        _clients = new CapturingClients();
        _clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        _services = new ServiceCollection()
            .AddSingleton<IHttpClientFactory>(_clients)
            .AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings(new IntegrationSettingsDto("ghp_test_token", _server.BaseAddress.AbsoluteUri, null, null, [])))
            .AddScoped<GitHubIssuesClient>()
            .BuildServiceProvider();
        _server.Respond($"{Repo}/commits/{Fix}", $$"""{ "sha": "{{Fix}}", "commit": { "committer": { "date": "{{GitHubTime(MergedAt)}}" } } }""");
    }

    [TearDown]
    public async Task StopTheStub()
    {
        await _services.DisposeAsync();
        await _server.DisposeAsync();
    }

    private GitHubImagePublications Publications() => new(_services.GetRequiredService<IServiceScopeFactory>(), _clock);

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
        => _server.Respond(RunsPath, $$"""{ "total_count": {{runs.Length}}, "workflow_runs": [{{string.Join(",", runs.Reverse())}}] }""");

    private void Compare(string head, string status)
        => _server.Respond($"{Repo}/compare/{Fix}...{head}", $$"""{ "status": "{{status}}", "ahead_by": 1, "behind_by": 0, "commits": [], "files": [] }""");

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

    // An IHttpClientFactory whose clients call the stub (accepting its self-signed certificate) and keep each request's path and query.
    private sealed class CapturingClients : IHttpClientFactory
    {
        private readonly ConcurrentQueue<string> _sent = new();

        public IReadOnlyList<string> Sent => [.. _sent];

        public HttpClient CreateClient(string name)
        {
            var inner = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            return new HttpClient(new Capture(this, inner)) { Timeout = TimeSpan.FromSeconds(15) };
        }

        private sealed class Capture(CapturingClients owner, HttpMessageHandler inner) : DelegatingHandler(inner)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner._sent.Enqueue(request.RequestUri!.PathAndQuery);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}

using ShipLean.Application.Features.TaskChanges.Hosts;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport.Http;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.TaskChanges.Hosts;

// GitHubCodeHost against a stub of the GitHub API: https for the token cases, plain http to show the token never goes there.
[TestFixture]
public class GitHubCodeHostTests
{
    private const string Sha = "a81d4e0b2c3d4e5f60718293a4b5c6d7e8f90123";
    private const string CommitPath = "/repos/acme/storefront/commits/" + Sha;
    private static readonly RepoRef Repo = new(CodeProvider.Github, "github.com", "acme/storefront");

    private HttpsStubServer _server = null!;
    private RecordingHttpClientFactory _clients = null!;

    [SetUp]
    public async Task StartServerAsync()
    {
        _server = await HttpsStubServer.StartAsync();
        _clients = new RecordingHttpClientFactory();
    }

    [TearDown]
    public async Task StopServerAsync() => await _server.DisposeAsync();

    private GitHubCodeHost HostOn(Uri api, string? token = "t0ken")
        => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(token, api.AbsoluteUri, null, null, [])));

    private const string CommitJson = $$"""
        { "sha": "{{Sha}}", "parents": [ { "sha": "9f8e7d6c5b4a39281706f5e4d3c2b1a098765432" } ],
          "commit": { "message": "Add retry\n\nLonger body", "author": { "name": "Mira", "date": "2026-11-12T08:30:00Z" } },
          "stats": { "additions": 7, "deletions": 3, "total": 10 },
          "files": [ { "filename": "src/cart.ts" }, { "filename": "src/pay.ts" } ] }
        """;

    [Test]
    public async Task GitHubCodeHost_WhenCommitRead_ShouldMapMetaPathsAndFullSha()
    {
        _server.Respond(CommitPath, CommitJson);

        var read = await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha[..7]);

        read.Status.ShouldBe(CommitReadStatus.Found);
        var meta = read.Meta!;
        meta.Sha.ShouldBe(Sha);
        meta.ParentShas.ShouldBe(["9f8e7d6c5b4a39281706f5e4d3c2b1a098765432"]);
        meta.Subject.ShouldBe("Add retry");
        meta.AuthorName.ShouldBe("Mira");
        meta.AuthoredAt.ShouldBe(new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.Zero));
        meta.Additions.ShouldBe(7);
        meta.Deletions.ShouldBe(3);
        meta.Paths.ShouldBe(["src/cart.ts", "src/pay.ts"]);
    }

    [Test]
    public async Task GitHubCodeHost_When404OnRepo_ShouldReturnUnreadable()
    {
        var read = await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha);

        read.Status.ShouldBe(CommitReadStatus.Unreadable);
        _server.Requests.ShouldBe([$"GET {CommitPath}", "GET /repos/acme/storefront"]);
    }

    [Test]
    public async Task GitHubCodeHost_When404OnCommitInAReadableRepo_ShouldReturnMissing()
    {
        _server.Respond("/repos/acme/storefront", "{}");

        (await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha)).Status.ShouldBe(CommitReadStatus.Missing);
    }

    [Test]
    public async Task GitHubCodeHost_When422OnCommit_ShouldReturnMissing()
    {
        _server.Respond(CommitPath, "{}", status: 422);

        (await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha)).Status.ShouldBe(CommitReadStatus.Missing);
    }

    [TestCase(401)]
    [TestCase(403)]
    [TestCase(500)]
    public async Task GitHubCodeHost_WhenTheHostRefuses_ShouldReturnUnreadable(int status)
    {
        _server.Respond(CommitPath, "{}", status: status);

        (await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha)).Status.ShouldBe(CommitReadStatus.Unreadable);
    }

    [Test]
    public async Task GitHubCodeHost_WhenTheHostIsDown_ShouldReturnUnreadable()
    {
        var down = _server.BaseAddress;
        await _server.DisposeAsync();

        (await HostOn(down).ReadCommitAsync(Repo, Sha)).Status.ShouldBe(CommitReadStatus.Unreadable);
        _server = await HttpsStubServer.StartAsync();
    }

    [Test]
    public async Task GitHubCodeHost_WhenHttpScheme_ShouldNotSendToken()
    {
        await using var http = await StubHttpServer.StartAsync();
        http.Respond(CommitPath, CommitJson, "application/json");

        var read = await HostOn(http.BaseAddress).ReadCommitAsync(Repo, Sha);

        read.Status.ShouldBe(CommitReadStatus.Found);
        _clients.Requests.ShouldNotBeEmpty();
        _clients.Requests.ShouldAllBe(r => r.Headers.Authorization == null);
    }

    [Test]
    public async Task GitHubCodeHost_WhenHttps_ShouldSendTokenAsBearer()
    {
        _server.Respond(CommitPath, CommitJson);

        await HostOn(_server.BaseAddress).ReadCommitAsync(Repo, Sha);

        _clients.Requests[0].Headers.Authorization!.ToString().ShouldBe("Bearer t0ken");
    }

    [Test]
    public async Task GitHubCodeHost_WhenPatchRead_ShouldAskForTheDiffFormat()
    {
        const string Diff = "diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-a\n+b\n";
        _server.Respond(CommitPath, Diff);

        var patch = await HostOn(_server.BaseAddress).ReadPatchAsync(Repo, Sha);

        patch.Status.ShouldBe(CommitReadStatus.Found);
        patch.UnifiedDiff.ShouldBe(Diff);
        _clients.Requests[0].Headers.Accept.Single().MediaType.ShouldBe("application/vnd.github.diff");
    }

    [Test]
    public async Task GitHubCodeHost_WhenRangeRead_ShouldCompareBaseToHead()
    {
        _server.Respond("/repos/acme/storefront/compare/aaa...bbb", "diff --git a/x b/x\n");

        var patch = await HostOn(_server.BaseAddress).ReadRangePatchAsync(Repo, "aaa", "bbb");

        patch.Status.ShouldBe(CommitReadStatus.Found);
        _server.Requests.ShouldBe(["GET /repos/acme/storefront/compare/aaa...bbb"]);
    }
}

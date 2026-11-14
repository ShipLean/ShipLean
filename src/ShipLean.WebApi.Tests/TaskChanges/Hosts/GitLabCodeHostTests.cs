using ShipLean.Application.Features.TaskChanges.Hosts;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.TaskChanges.Hosts;

// GitLabCodeHost against a stub of the v4 API: the per-file JSON becomes git unified text.
[TestFixture]
public class GitLabCodeHostTests
{
    private const string Sha = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";
    private const string Project = "/api/v4/projects/acme%2Fstorefront";
    private static readonly RepoRef Repo = new(CodeProvider.Gitlab, "gitlab.example", "acme/storefront");

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

    private GitLabCodeHost Host() => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(null, "https://api.github.com", _server.BaseAddress.AbsoluteUri, "glpat", [])));

    [Test]
    public async Task GitLabCodeHost_WhenDiffRead_ShouldReturnGitUnifiedText()
    {
        _server.Respond($"{Project}/repository/commits/{Sha}/diff", """
            [ { "old_path": "src/a.ts", "new_path": "src/a.ts", "a_mode": "100644", "b_mode": "100644", "new_file": false, "renamed_file": false, "deleted_file": false, "diff": "@@ -1 +1 @@\n-a\n+b\n" },
              { "old_path": "src/old.ts", "new_path": "src/new.ts", "a_mode": "100644", "b_mode": "100644", "new_file": false, "renamed_file": true, "deleted_file": false, "diff": "@@ -1 +1 @@\n-x\n+y" } ]
            """);

        var patch = await Host().ReadPatchAsync(Repo, Sha);

        patch.Status.ShouldBe(CommitReadStatus.Found);
        patch.UnifiedDiff.ShouldBe(
            "diff --git a/src/a.ts b/src/a.ts\n--- a/src/a.ts\n+++ b/src/a.ts\n@@ -1 +1 @@\n-a\n+b\n"
            + "diff --git a/src/old.ts b/src/new.ts\nrename from src/old.ts\nrename to src/new.ts\n--- a/src/old.ts\n+++ b/src/new.ts\n@@ -1 +1 @@\n-x\n+y\n");
    }

    [Test]
    public async Task GitLabCodeHost_WhenRootCommit_ShouldDiffAgainstEmptyTree()
    {
        _server.Respond($"{Project}/repository/commits/{Sha}/diff", """
            [ { "old_path": "README.md", "new_path": "README.md", "a_mode": "0", "b_mode": "100644", "new_file": true, "renamed_file": false, "deleted_file": false, "diff": "@@ -0,0 +1 @@\n+hello\n" } ]
            """);

        var patch = await Host().ReadPatchAsync(Repo, Sha);

        patch.UnifiedDiff.ShouldBe("diff --git a/README.md b/README.md\nnew file mode 100644\n--- /dev/null\n+++ b/README.md\n@@ -0,0 +1 @@\n+hello\n");
    }

    [Test]
    public async Task GitLabCodeHost_WhenCommitRead_ShouldMapMetaAndPaths()
    {
        _server.Respond($"{Project}/repository/commits/{Sha}", $$"""
            { "id": "{{Sha}}", "parent_ids": [ "1111111111111111111111111111111111111111" ], "title": "Fix totals", "author_name": "Tom",
              "authored_date": "2026-11-12T08:30:00.000+00:00", "stats": { "additions": 4, "deletions": 1, "total": 5 } }
            """);
        _server.Respond($"{Project}/repository/commits/{Sha}/diff", """
            [ { "old_path": "a.ts", "new_path": "a.ts", "new_file": false, "renamed_file": false, "deleted_file": false, "diff": "" },
              { "old_path": "gone.ts", "new_path": "gone.ts", "new_file": false, "renamed_file": false, "deleted_file": true, "diff": "" } ]
            """);

        var read = await Host().ReadCommitAsync(Repo, Sha);

        read.Status.ShouldBe(CommitReadStatus.Found);
        read.Meta!.Sha.ShouldBe(Sha);
        read.Meta.Subject.ShouldBe("Fix totals");
        read.Meta.AuthorName.ShouldBe("Tom");
        read.Meta.Additions.ShouldBe(4);
        read.Meta.Deletions.ShouldBe(1);
        read.Meta.ParentShas.ShouldBe(["1111111111111111111111111111111111111111"]);
        read.Meta.Paths.ShouldBe(["a.ts", "gone.ts"]);
        _clients.Requests[0].Headers.GetValues("PRIVATE-TOKEN").ShouldBe(["glpat"]);
    }

    [Test]
    public async Task GitLabCodeHost_When404OnProject_ShouldReturnUnreadable()
    {
        var read = await Host().ReadCommitAsync(Repo, Sha);

        read.Status.ShouldBe(CommitReadStatus.Unreadable);
    }

    [Test]
    public async Task GitLabCodeHost_When404OnCommitInAReadableProject_ShouldReturnMissing()
    {
        _server.Respond(Project, "{}");

        (await Host().ReadCommitAsync(Repo, Sha)).Status.ShouldBe(CommitReadStatus.Missing);
    }

    [Test]
    public async Task GitLabCodeHost_WhenRangeRead_ShouldCompareFromTo()
    {
        _server.Respond($"{Project}/repository/compare", """{ "diffs": [ { "old_path": "a", "new_path": "a", "diff": "@@ -1 +1 @@\n-1\n+2\n" } ] }""");

        var patch = await Host().ReadRangePatchAsync(Repo, "aaa", "bbb");

        patch.UnifiedDiff.ShouldBe("diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1 +1 @@\n-1\n+2\n");
    }
}

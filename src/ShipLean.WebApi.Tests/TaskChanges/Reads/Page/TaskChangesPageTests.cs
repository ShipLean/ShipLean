using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Reads.Page;

// GET api/work-items/{id}/changes on the real host and database, over FakeCodeHost's real git patches.
[TestFixture]
public class TaskChangesPageTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly RepoRef Storefront = FakeCodeHost.StorefrontRepo;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddFakeCodeHost();

    [Test]
    public async Task ChangesAll_WhenCommitsChain_ShouldReturnOneCombinedPart()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.Second, FakeCodeHost.Shas.Third);

        var page = await GetAsync(item, "all");

        page.Mode.ShouldBe("combined");
        page.Selected.ShouldBe("all");
        var part = page.Parts.ShouldHaveSingleItem();
        (part.Repo, part.Sha, part.BaseSha, part.HeadSha).ShouldBe((Storefront.Name, null, FakeCodeHost.Shas.Root, FakeCodeHost.Shas.Third));
        part.Files.ShouldNotBeEmpty();
        page.Commits.Count.ShouldBe(3);
    }

    [Test]
    public async Task ChangesAll_WhenAnotherCommitBetween_ShouldReturnEachCommit()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.OtherBranch, FakeCodeHost.Shas.Second, FakeCodeHost.Shas.Third);

        var page = await GetAsync(item, "all");

        page.Mode.ShouldBe("perCommit");
        page.Parts.Select(p => p.Sha).ShouldBe([FakeCodeHost.Shas.First, FakeCodeHost.Shas.OtherBranch, FakeCodeHost.Shas.Second, FakeCodeHost.Shas.Third]);
        page.Parts.ShouldAllBe(p => p.BaseSha == null);
    }

    [Test]
    public async Task ChangesAll_WhenFirstIsRootCommit_ShouldReturnEachCommit()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.Root, FakeCodeHost.Shas.First);

        var page = await GetAsync(item, "all");

        page.Mode.ShouldBe("perCommit");
        page.Parts.Select(p => p.Sha).ShouldBe([FakeCodeHost.Shas.Root, FakeCodeHost.Shas.First]);
    }

    [Test]
    public async Task ChangesAll_WhenTwoRepos_ShouldReturnPartPerRepo()
    {
        var (item, _, seed) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.Second);
        await seed.CommitAsync(item, FakeCodeHost.Payments, FakeCodeHost.Shas.PaymentsOnly);

        var page = await GetAsync(item, "all");

        page.Mode.ShouldBe("perCommit");
        page.Parts.Select(p => p.Repo).ShouldBe([Storefront.Name, FakeCodeHost.Payments.Name]);
        page.Parts[0].BaseSha.ShouldBe(FakeCodeHost.Shas.Root);
        page.Parts[0].HeadSha.ShouldBe(FakeCodeHost.Shas.Second);
        page.Parts[1].Sha.ShouldBe(FakeCodeHost.Shas.PaymentsOnly);
    }

    [Test]
    public async Task ChangesSingle_WhenCommitId_ShouldReturnThatCommitOnly()
    {
        var (item, commits, _) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.Second);

        var page = await GetAsync(item, commits[1].ToString());

        page.Mode.ShouldBe("single");
        page.Selected.ShouldBe(commits[1].ToString());
        var part = page.Parts.ShouldHaveSingleItem();
        (part.Sha, part.BaseSha, part.HeadSha).ShouldBe((FakeCodeHost.Shas.Second, null, FakeCodeHost.Shas.Second));
        page.Commits.Count.ShouldBe(2);
    }

    [Test]
    public async Task ChangesSingle_WhenSameShaInTwoRepos_ShouldPickByCommitId()
    {
        var (item, commits, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await CopyCommitAsync(item, FakeCodeHost.Payments, FakeCodeHost.Shas.First, FakeCodeHost.Shas.First, 0);

        var page = await GetAsync(item, commits[0].ToString());

        var part = page.Parts.ShouldHaveSingleItem();
        part.Repo.ShouldBe(Storefront.Name);
        page.Commits.Count.ShouldBe(2);
    }

    [Test]
    public async Task ChangesSingle_WhenUnknownCommitId_ShouldReturnNotFound()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);

        using var response = await Api.GetAsync($"api/work-items/{item}/changes?commit={Guid.NewGuid()}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ChangesSingle_WhenNotAGuid_ShouldReturnNotFound()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);

        using var response = await Api.GetAsync($"api/work-items/{item}/changes?commit={FakeCodeHost.Shas.First}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Changes_WhenRepoUnreadable_ShouldMarkAccessAndKeepRows()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await CopyCommitAsync(item, FakeCodeHost.SecretRepo, FakeCodeHost.Shas.First, FakeCodeHost.Shas.First, 2);

        var page = await GetAsync(item, "all");

        page.Commits.Select(c => (c.Repo, c.Access)).ShouldBe([(Storefront.Name, "ok"), (FakeCodeHost.Secret, "unreadable")]);
        page.Parts.Select(p => p.Repo).ShouldBe([Storefront.Name]);
    }

    [Test]
    public async Task Changes_WhenCommitMissing_ShouldMarkMissing()
    {
        var (item, _, _) = await ArrangeAsync(FakeCodeHost.Shas.First);
        await CopyCommitAsync(item, Storefront, FakeCodeHost.Shas.First, FakeCodeHost.Shas.Missing, 2);

        var page = await GetAsync(item, "all");

        page.Commits.Select(c => c.Access).ShouldBe(["ok", "missing"]);
        page.Parts.Select(p => p.Sha).ShouldBe([FakeCodeHost.Shas.First]);
    }

    [Test]
    public async Task Changes_WhenSamePatch_ShouldKeepPatchIdAcrossHeads()
    {
        var (item, _, seed) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.Second);
        var before = await GetAsync(item, "all");
        await seed.CommitAsync(item, Storefront, FakeCodeHost.Shas.Third);

        var after = await GetAsync(item, "all");

        after.Parts.Single().HeadSha.ShouldNotBe(before.Parts.Single().HeadSha);
        var checkoutBefore = before.Parts.Single().Files.Single(f => f.Path == "src/checkout.ts");
        var checkoutAfter = after.Parts.Single().Files.Single(f => f.Path == "src/checkout.ts");
        checkoutAfter.PatchId.ShouldBe(checkoutBefore.PatchId);
        after.Parts.Single().Files.Single(f => f.Path == "src/basket.ts").PatchId.ShouldNotBe(before.Parts.Single().Files.Single(f => f.Path == "src/basket.ts").PatchId);
    }

    [Test]
    public async Task Changes_WhenFetched_ShouldReturnHeadAsLastPositionCommit()
    {
        var (item, _, seed) = await ArrangeAsync();
        var later = await seed.CommitAsync(item, Storefront, FakeCodeHost.Shas.Second, position: 9);
        await seed.CommitAsync(item, Storefront, FakeCodeHost.Shas.First, position: 5);

        var page = await GetAsync(item, "all");

        page.HeadCommitId.ShouldBe(later.ToString());
    }

    [Test]
    public async Task Changes_WhenNoCommits_ShouldReturnEmptyPage()
    {
        var (item, _, _) = await ArrangeAsync();

        var page = await GetAsync(item, "all");

        page.Commits.ShouldBeEmpty();
        page.Parts.ShouldBeEmpty();
        page.HeadCommitId.ShouldBeNull();
    }

    [Test]
    public async Task Changes_WhenFeature_ShouldReturnNotTaskOrBug()
    {
        using var response = await Api.GetAsync($"api/work-items/{SeedIds.WorkItems.Ship101}/changes?commit=all");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.NotTaskOrBug);
    }

    [Test]
    public async Task Changes_WhenBug_ShouldReturnItemFields()
    {
        await using var database = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bug").WithStateNamed("Active").InsertAsync(database);

        var page = await GetAsync(item, "all");

        page.ItemType.ShouldBe("bug");
        page.StateCategory.ShouldBe("inProgress");
        page.ItemKey.ShouldStartWith("SHIP-");
    }

    [Test]
    public async Task Changes_WhenOpenComments_ShouldCountPerFileAndOverall()
    {
        var (item, _, seed) = await ArrangeAsync(FakeCodeHost.Shas.First);
        var open = await seed.CommentAsync(item, Storefront, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        await seed.ReplyAsync(item, open, "Because");
        var done = await seed.CommentAsync(item, Storefront, FakeCodeHost.Shas.First, "src/checkout.ts", CodeSide.New, 1, "Done one");
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE code_comment SET resolved_at = now() WHERE id = @done", new { done });

        var page = await GetAsync(item, "all");

        page.OpenComments.ShouldBe(1);
        page.Comments.Count.ShouldBe(2);
        page.Comments.Single(c => c.Id == open).Replies.Count.ShouldBe(1);
        var files = page.Parts.Single().Files;
        files.Single(f => f.Path == "src/cart.ts").OpenComments.ShouldBe(1);
        files.Single(f => f.Path == "src/checkout.ts").OpenComments.ShouldBe(0);
    }

    [Test]
    public async Task Changes_WhenFetched_ShouldMatchFixtureShape()
    {
        var (item, _, seed) = await ArrangeAsync(FakeCodeHost.Shas.First, FakeCodeHost.Shas.OtherBranch);
        var comment = await seed.CommentAsync(item, Storefront, FakeCodeHost.Shas.First, "src/cart.ts", CodeSide.New, 1, "Why?");
        await seed.ReplyAsync(item, comment, "Because");

        using var response = await Api.GetAsync($"api/work-items/{item}/changes?commit=all");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var fixture = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "taskChanges", "changes-per-commit.json")))!["changesPerCommit"]!;

        Keys(body).ShouldBe(Keys(fixture));
        Keys(body["commits"]![0]!).ShouldBe(Keys(fixture["commits"]![0]!));
        Keys(body["parts"]![0]!).ShouldBe(Keys(fixture["parts"]![0]!));
        Keys(body["parts"]![0]!["files"]![0]!).ShouldBe(Keys(fixture["parts"]![0]!["files"]![0]!));
        Keys(body["comments"]![0]!).ShouldBe(Keys(fixture["comments"]![0]!));
        Keys(body["comments"]![0]!["replies"]![0]!).ShouldBe(Keys(fixture["comments"]![0]!["replies"]![0]!));
    }

    private static List<string> Keys(JsonNode node) => [.. node.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal)];

    private async Task<TaskChangesDto> GetAsync(Guid item, string commit)
    {
        using var response = await Api.GetAsync($"api/work-items/{item}/changes?commit={commit}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<TaskChangesDto>(text, Wire)!;
    }

    // A task in Active with the given storefront commits, oldest first.
    private async Task<(Guid Item, List<Guid> Commits, CodeChangesSeed Seed)> ArrangeAsync(params string[] shas)
    {
        var database = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithStateNamed("Active").InsertAsync(database);
        var seed = new CodeChangesSeed(database);
        var commits = new List<Guid>();
        foreach (var sha in shas)
        {
            commits.Add(await seed.CommitAsync(item, Storefront, sha));
        }

        return (item, commits, seed);
    }

    // A commit row in another repository (or under another sha) that the fake host has no such commit for: the meta is copied from a storefront commit.
    private async Task CopyCommitAsync(Guid item, RepoRef repo, string copyOf, string asSha, int position)
    {
        await using var database = await OpenDatabaseAsync();
        var repository = await new CodeChangesSeed(database).RepositoryAsync(repo);
        await database.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            SELECT @repository, @asSha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at FROM code_commit WHERE sha = @copyOf LIMIT 1
            ON CONFLICT DO NOTHING;
            INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via)
            VALUES (@id, @item, @repository, @asSha, @position, 'add_commits');
            """,
            new { repository, copyOf, asSha, id = Guid.NewGuid(), item, position });
    }
}

using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TaskChanges;
using static ShipLean.TestSupport.TaskChanges.FakeCodeHost;

namespace ShipLean.WebApi.Tests.TaskChanges.Commits;

// ITaskCommits.AddAsync against the fake code host, reading the tables to prove what was written.
[TestFixture]
public class AddCommitsTests : ApiFixture
{
    private const string Storefront = "https://github.com/acme/storefront";
    private const string PaymentsUrl = "https://github.com/acme/payments";
    private const string SecretUrl = "https://github.com/acme/secret";

    private readonly RecordingCodeComments _comments = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddFakeCodeHost();
        services.RemoveAll<ICodeComments>();
        services.AddSingleton<ICodeComments>(_comments);
    };

    [SetUp]
    public void ClearRecorder() => _comments.Marked.Clear();

    [Test]
    public async Task AddCommits_WhenTask_ShouldStoreInOrderWithMetaAndPaths()
    {
        var item = await NewItemAsync("task");

        var outcome = await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First), new CommitRef(Storefront, Shas.Second));

        outcome.Added.Select(c => c.Position).ShouldBe([1, 2]);
        outcome.Added.Select(c => c.Sha).ShouldBe([Shas.First, Shas.Second]);
        var listed = await ListAsync(item);
        listed.Select(c => c.Sha).ShouldBe([Shas.First, Shas.Second]);
        var meta = MetaOf(FakeCodeHost.Storefront, Shas.Second);
        listed[1].Subject.ShouldBe(meta.Subject);
        listed[1].Paths.ShouldBe(meta.Paths);
        listed[1].Paths.ShouldNotBeEmpty();
        listed[1].Additions.ShouldBe(meta.Additions);
        listed[1].AddedVia.ShouldBe(CommitSource.AddCommits);
    }

    [Test]
    public async Task AddCommits_WhenBug_ShouldStore()
    {
        var item = await NewItemAsync("bug");

        var outcome = await AddAsync(item, CommitSource.Form, new CommitRef(Storefront, Shas.First));

        outcome.Added.ShouldHaveSingleItem();
        (await ListAsync(item)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task AddCommits_WhenStory_ShouldRefuseNamingItsTasks()
    {
        var story = await NewItemAsync("story");
        var child = await NewItemAsync("task", story);
        var key = await KeyOfAsync(child);

        var refused = async () => await AddAsync(story, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First));

        var error = await refused.ShouldThrowAsync<ApiException>();
        error.Code.ShouldBe(ApiErrorCodes.NotTaskOrBug);
        error.Message.ShouldContain(key);
        (await CountRowsAsync(story)).ShouldBe(0);
    }

    [Test]
    public async Task AddCommits_WhenAlreadyLinked_ShouldSkip()
    {
        var item = await NewItemAsync("task");
        await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First));

        var again = await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First[..7]), new CommitRef(Storefront, Shas.Second));

        again.Skipped.Select(c => c.Sha).ShouldBe([Shas.First]);
        again.Added.Select(c => c.Sha).ShouldBe([Shas.Second]);
        again.Added[0].Position.ShouldBe(2);
        (await CountRowsAsync(item)).ShouldBe(2);
    }

    [Test]
    public async Task AddCommits_WhenOneRepoUnreadable_ShouldWriteNothing()
    {
        var item = await NewItemAsync("task");

        var refused = async () => await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First), new CommitRef(SecretUrl, Shas.First));

        (await refused.ShouldThrowAsync<ApiException>()).Code.ShouldBe(ApiErrorCodes.RepoUnreadable);
        (await CountRowsAsync(item)).ShouldBe(0);
        _comments.Marked.ShouldBeEmpty();
    }

    [Test]
    public async Task AddCommits_WhenCommitMissing_ShouldRefuseBadSha()
    {
        var item = await NewItemAsync("task");

        var refused = async () => await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.Missing));

        (await refused.ShouldThrowAsync<ApiException>()).Code.ShouldBe(ApiErrorCodes.BadSha);
        (await CountRowsAsync(item)).ShouldBe(0);
    }

    [Test]
    public async Task AddCommits_WhenShortSha_ShouldStoreFullSha()
    {
        var item = await NewItemAsync("task");

        await AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.Third[..8]));

        (await ListAsync(item)).ShouldHaveSingleItem().Sha.ShouldBe(Shas.Third);
    }

    [Test]
    public async Task AddCommits_WhenAdded_ShouldWriteOneRevisionWithLabel()
    {
        var item = await NewItemAsync("task");
        var before = await RevisionsAsync(item);
        var events = new List<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, e => Record(events, e));

        var outcome = await AddAsync(item, CommitSource.ReportTask, new CommitRef(Storefront, Shas.First), new CommitRef(PaymentsUrl, Shas.PaymentsOnly));

        var after = await RevisionsAsync(item);
        after.Count.ShouldBe(before.Count + 1);
        after[^1].ShouldContain("Commits +2 (acme/storefront, acme/payments)");
        events.OfType<CommitsChanged>().Where(e => e.WorkItemId == item).ShouldHaveSingleItem();
        _comments.Marked.ShouldHaveSingleItem().Select(c => c.Sha).ShouldBe(outcome.Added.Select(c => c.Sha));
    }

    [Test]
    public async Task AddCommits_WhenMigration_ShouldWriteNoRevisionOrEvent()
    {
        var item = await NewItemAsync("task");
        var before = await RevisionsAsync(item);
        var events = new List<ProjectEvent>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, e => Record(events, e));

        await AddAsync(item, CommitSource.Migration, new CommitRef(Storefront, Shas.First));

        (await CountRowsAsync(item)).ShouldBe(1);
        (await RevisionsAsync(item)).Count.ShouldBe(before.Count);
        events.OfType<CommitsChanged>().ShouldBeEmpty();
        _comments.Marked.ShouldBeEmpty();
    }

    [Test]
    public async Task AddCommits_WhenMigrationWithNoRequestUser_ShouldStoreWithNullAddedBy()
    {
        var item = await NewItemAsync("task");
        Host.ActAs(null);

        await AddAsync(item, CommitSource.Migration, new CommitRef(Storefront, Shas.First));

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item_commit WHERE work_item_id = @item AND added_by IS NULL AND added_via = 'migration'", new { item })).ShouldBe(1);
    }

    [Test]
    public async Task AddCommits_WhenTwoCallsRace_ShouldNotDuplicate()
    {
        var item = await NewItemAsync("task");

        await Task.WhenAll(
            AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First)),
            AddAsync(item, CommitSource.AddCommits, new CommitRef(Storefront, Shas.First)));

        (await CountRowsAsync(item)).ShouldBe(1);
    }

    [Test]
    public async Task AddCommitForm_WhenCommitUrl_ShouldParseRepoAndSha()
    {
        var item = await NewItemAsync("task");

        using var response = await Api.PostJsonAsync($"api/work-items/{item}/commits", new { repo = (string?)null, sha = $"{Storefront}/commit/{Shas.Second}" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var listed = await ListAsync(item);
        listed.ShouldHaveSingleItem().Sha.ShouldBe(Shas.Second);
        listed[0].Repo.Path.ShouldBe(FakeCodeHost.Storefront);
        listed[0].AddedVia.ShouldBe(CommitSource.Form);
    }

    [Test]
    public async Task AddCommitForm_WhenNotMember_ShouldReturnNotFound()
    {
        var item = await NewItemAsync("task");

        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"api/work-items/{item}/commits", new { repo = Storefront, sha = Shas.First });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await CountRowsAsync(item)).ShouldBe(0);
    }

    private static Task Record(List<ProjectEvent> events, ProjectEvent projectEvent)
    {
        events.Add(projectEvent);
        return Task.CompletedTask;
    }

    private async Task<AddCommitsOutcome> AddAsync(Guid item, CommitSource via, params CommitRef[] commits)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITaskCommits>().AddAsync(item, commits, via);
    }

    private async Task<IReadOnlyList<TaskCommit>> ListAsync(Guid item)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITaskCommits>().ListAsync(item);
    }

    private async Task<Guid> NewItemAsync(string type, Guid? parent = null)
    {
        await using var connection = await OpenDatabaseAsync();
        var builder = new WorkItemBuilder(SeedIds.Projects.Ship).WithType(type);
        return await (parent is { } p ? builder.WithParent(p) : builder).InsertAsync(connection);
    }

    private async Task<string> KeyOfAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item }) ?? string.Empty;
    }

    private async Task<int> CountRowsAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item_commit WHERE work_item_id = @item AND archived_at IS NULL", new { item });
    }

    private async Task<List<string>> RevisionsAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<string>("SELECT changes::text FROM revision WHERE work_item_id = @item ORDER BY rev", new { item })];
    }

    private sealed class RecordingCodeComments : ICodeComments
    {
        public IList<IReadOnlyList<TaskCommit>> Marked { get; } = [];

        public Task MarkOutdatedAsync(Guid workItemId, IReadOnlyList<TaskCommit> added, CancellationToken cancellationToken = default)
        {
            Marked.Add(added);
            return Task.CompletedTask;
        }

        public Task<CodeComment> AddAsync(Guid workItemId, NewCodeComment comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CodeComment> ReplyAsync(Guid workItemId, Guid commentId, string body, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ResolveAsync(Guid workItemId, Guid commentId, bool resolved, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CodeComment>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<CodeComment>> OpenAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AnswerAsync(Guid workItemId, IReadOnlyList<CodeCommentAnswer> answers, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

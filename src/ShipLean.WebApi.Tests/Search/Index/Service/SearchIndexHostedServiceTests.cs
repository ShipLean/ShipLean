using Microsoft.Extensions.DependencyInjection;
using Dapper;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

[TestFixture]
public class SearchIndexHostedServiceTests : ServiceFixture
{
    [Test]
    public async Task Service_WhenWorkItemUpdated_ShouldSyncThatItem()
    {
        var item = await ArrangeItemAsync();

        var sources = await PublishAsync(new WorkItemUpdated(Project, item, 2, ["title"], false));

        sources.ShouldContain(new SearchSource(SearchSourceKind.Item, item));
    }

    [TestCase("title")]
    [TestCase("state")]
    public async Task Service_WhenWorkItemUpdatedTitleOrState_ShouldAlsoSyncItsCommentsAndAttachments(string field)
    {
        var item = await ArrangeItemAsync();
        var comment = await ArrangeCommentAsync(item);
        var attachment = await ArrangeAttachmentAsync(item);
        var other = await ArrangeCommentAsync(await ArrangeItemAsync());

        var sources = await PublishAsync(new WorkItemUpdated(Project, item, 2, [field], false));

        sources.ShouldBe(
            [new SearchSource(SearchSourceKind.Item, item), new SearchSource(SearchSourceKind.Comment, comment), new SearchSource(SearchSourceKind.Attachment, attachment)],
            ignoreOrder: true);
        sources.ShouldNotContain(new SearchSource(SearchSourceKind.Comment, other));
    }

    [Test]
    public async Task Service_WhenWorkItemUpdatedOtherFields_ShouldSyncOnlyTheItem()
    {
        var item = await ArrangeItemAsync();
        await ArrangeCommentAsync(item);
        await ArrangeAttachmentAsync(item);

        var sources = await PublishAsync(new WorkItemUpdated(Project, item, 2, ["priority", "assignee"], false));

        sources.ShouldBe([new SearchSource(SearchSourceKind.Item, item)]);
    }

    [TestCase("created")]
    [TestCase("removed")]
    public async Task Service_WhenWorkItemCreatedOrRemoved_ShouldSyncItAndItsComments(string kind)
    {
        var item = await ArrangeItemAsync();
        var comment = await ArrangeCommentAsync(item);
        ProjectEvent projectEvent = kind == "created"
            ? new WorkItemCreated(Project, item, "SHIP-1", default, null)
            : new WorkItemRemoved(Project, item, null);

        var sources = await PublishAsync(projectEvent);

        sources.ShouldBe([new SearchSource(SearchSourceKind.Item, item), new SearchSource(SearchSourceKind.Comment, comment)], ignoreOrder: true);
    }

    [Test]
    public async Task Service_WhenCommentsChanged_ShouldSyncThoseComments()
    {
        var item = await ArrangeItemAsync();
        var first = await ArrangeCommentAsync(item);
        var second = await ArrangeCommentAsync(item);
        Guid archived;
        await using (var connection = await OpenDatabaseAsync())
        {
            archived = await new CommentBuilder(item).Archived().InsertAsync(connection);
        }

        var sources = await PublishAsync(new CommentsChanged(Project, item));

        sources.ShouldBe(
            [new SearchSource(SearchSourceKind.Comment, first), new SearchSource(SearchSourceKind.Comment, second), new SearchSource(SearchSourceKind.Comment, archived)],
            ignoreOrder: true);
    }

    [Test]
    public async Task Service_WhenAttachmentsChanged_ShouldSyncThoseAttachments()
    {
        var item = await ArrangeItemAsync();
        var attachment = await ArrangeAttachmentAsync(item);

        var sources = await PublishAsync(new AttachmentsChanged(Project, item));

        sources.ShouldBe([new SearchSource(SearchSourceKind.Attachment, attachment)]);
    }

    [Test]
    public async Task Service_WhenSprintChanged_ShouldSyncThatSprint()
    {
        var sprint = Guid.NewGuid();

        var sources = await PublishAsync(new SprintChanged(Project, sprint));

        sources.ShouldBe([new SearchSource(SearchSourceKind.Sprint, sprint)]);
    }

    [Test]
    public async Task Service_WhenProjectChanged_ShouldSyncThatProject()
    {
        var sources = await PublishAsync(new ProjectChanged(Project));

        sources.ShouldBe([new SearchSource(SearchSourceKind.Project, Project)]);
    }

    [Test]
    public async Task Service_WhenPassThrows_ShouldKeepRunning()
    {
        Indexer.FailNext(1);
        var failing = Guid.NewGuid();
        await Host.Services.GetRequiredService<IProjectEvents>().PublishAsync(new SprintChanged(Project, failing));
        (await Writer.NextSyncAsync()).ShouldBe([new SearchSource(SearchSourceKind.Sprint, failing)]);
        await Indexer.NextPassAsync();

        var next = Guid.NewGuid();
        var sources = await PublishAsync(new SprintChanged(Project, next));

        sources.ShouldBe([new SearchSource(SearchSourceKind.Sprint, next)]);
        Writer.SyncAllCalls.ShouldBe(0);
    }

    private async Task<Guid> ArrangeItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(Project).WithTitle("Service item").InsertAsync(connection);
    }

    private async Task<Guid> ArrangeCommentAsync(Guid item)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new CommentBuilder(item).WithBody("A comment.").InsertAsync(connection);
    }

    private async Task<Guid> ArrangeAttachmentAsync(Guid item)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, search_text) VALUES (@id, @item, @id::text || '.md', 'text/markdown', 3, 'blob/f', 'text')",
            new { id, item });
        return id;
    }
}

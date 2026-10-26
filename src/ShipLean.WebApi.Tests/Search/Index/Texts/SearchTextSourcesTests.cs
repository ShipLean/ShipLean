using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Texts;

// The real comment, attachment, project and sprint text sources behind the real writer. Entities are arranged with builders and
// direct SQL (SyncAll would delete rows of sources that don't exist), the writer is called directly and the assertions read search_doc.
[TestFixture]
public class SearchTextSourcesTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [Test]
    public async Task Index_WhenCommentSynced_ShouldWriteCommentRowsUnderItsItem()
    {
        var item = await ArrangeItemAsync("Checkout fails");
        var comment = await ArrangeCommentAsync(item, "  Reproduced on staging.  ");

        await SyncAsync(SearchSourceKind.Comment, comment);

        var rows = await RowsAsync("comment", comment);
        var row = rows.ShouldHaveSingleItem();
        row.Field.ShouldBe("comment");
        row.ChunkNo.ShouldBe(0);
        row.Text.ShouldBe("Checkout fails\nReproduced on staging.");
        row.WorkItemId.ShouldBe(item);
        row.ProjectId.ShouldBe(Project);
        row.TextHash.ShouldBe(SearchDocBuilder.HashOf(row.Text));
    }

    [Test]
    public async Task Index_WhenCommentArchived_ShouldDropItsRows()
    {
        var item = await ArrangeItemAsync("Archived comment item");
        var comment = await ArrangeCommentAsync(item, "Soon archived.");
        await SyncAsync(SearchSourceKind.Comment, comment);
        (await RowsAsync("comment", comment)).Count.ShouldBe(1);

        await ExecuteAsync("UPDATE comment SET archived_at = now() WHERE id = @id", new { id = comment });
        await SyncAsync(SearchSourceKind.Comment, comment);

        (await RowsAsync("comment", comment)).ShouldBeEmpty();
    }

    [Test]
    public async Task Index_WhenCommentItemRemoved_ShouldDropItsRows()
    {
        var item = await ArrangeItemAsync("Removed item");
        var comment = await ArrangeCommentAsync(item, "On a doomed item.");
        await SyncAsync(SearchSourceKind.Comment, comment);

        await ExecuteAsync("UPDATE work_item SET state_id = @removed WHERE id = @item", new { removed = SeedIds.States.Ship.StoryRemoved, item });
        await SyncAsync(SearchSourceKind.Comment, comment);

        (await RowsAsync("comment", comment)).ShouldBeEmpty();
    }

    [Test]
    public async Task Index_WhenAttachmentSynced_ShouldWriteAttachmentRowsUnderItsItem()
    {
        var item = await ArrangeItemAsync("Spec upload");
        var withText = await ArrangeAttachmentAsync(item, "The extracted spec text.");
        var withoutText = await ArrangeAttachmentAsync(item, null);

        await SyncAsync(SearchSourceKind.Attachment, withText, withoutText);

        var row = (await RowsAsync("attachment", withText)).ShouldHaveSingleItem();
        row.Field.ShouldBe("attachment");
        row.Text.ShouldBe("Spec upload\nThe extracted spec text.");
        row.WorkItemId.ShouldBe(item);
        row.ProjectId.ShouldBe(Project);
        (await RowsAsync("attachment", withoutText)).ShouldBeEmpty();
    }

    [Test]
    public async Task Index_WhenAttachmentArchived_ShouldDropItsRows()
    {
        var item = await ArrangeItemAsync("Archived attachment item");
        var attachment = await ArrangeAttachmentAsync(item, "Some text.");
        await SyncAsync(SearchSourceKind.Attachment, attachment);
        (await RowsAsync("attachment", attachment)).Count.ShouldBe(1);

        await ExecuteAsync("UPDATE attachment SET archived_at = now() WHERE id = @id", new { id = attachment });
        await SyncAsync(SearchSourceKind.Attachment, attachment);

        (await RowsAsync("attachment", attachment)).ShouldBeEmpty();
    }

    [Test]
    public async Task Index_WhenProjectSynced_ShouldWriteLexicalOnlyRow()
    {
        await ExecuteAsync("UPDATE project SET description = 'The storefront.' WHERE id = @Project", new { Project });
        var (key, name) = await QueryProjectAsync();

        await SyncAsync(SearchSourceKind.Project, Project);

        var row = (await RowsAsync("project", Project)).ShouldHaveSingleItem();
        row.Field.ShouldBe("project");
        row.Text.ShouldBe($"{key} {name}\nThe storefront.");
        row.WorkItemId.ShouldBeNull();
        row.ProjectId.ShouldBe(Project);
        row.Embedded.ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var tsv = await connection.ExecuteScalarAsync<string>("SELECT tsv::text FROM search_doc WHERE source_id = @Project", new { Project });
        tsv.ShouldNotBeNull().ShouldContain(":1A");
    }

    [Test]
    public async Task Index_WhenSprintSynced_ShouldWriteGoalAndNameRows()
    {
        var sprint = await ArrangeSprintAsync("Sprint Alpha", "Launch checkout");
        var noGoal = await ArrangeSprintAsync("Sprint Beta", "   ");

        await SyncAsync(SearchSourceKind.Sprint, sprint, noGoal);

        var rows = await RowsAsync("sprint", sprint);
        rows.Select(r => (r.Field, r.Text)).ShouldBe([("sprint_goal", "Launch checkout"), ("sprint_name", "Sprint Alpha")], ignoreOrder: true);
        rows.ShouldAllBe(r => r.WorkItemId == null && r.ProjectId == Project);
        (await RowsAsync("sprint", noGoal)).Select(r => r.Field).ShouldBe(["sprint_name"]);
    }

    [Test]
    public async Task Index_WhenSprintArchived_ShouldDropItsRows()
    {
        var sprint = await ArrangeSprintAsync("Sprint Gamma", "Soon archived");
        await SyncAsync(SearchSourceKind.Sprint, sprint);
        (await RowsAsync("sprint", sprint)).Count.ShouldBe(2);

        await ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @id", new { id = sprint });
        await SyncAsync(SearchSourceKind.Sprint, sprint);

        (await RowsAsync("sprint", sprint)).ShouldBeEmpty();
    }

    [Test]
    public async Task Index_WhenAllIdsRequested_ShouldReturnOnlyQualifyingSources()
    {
        var item = await ArrangeItemAsync("All ids item");
        var removed = await ArrangeItemAsync("All ids removed");
        await ExecuteAsync("UPDATE work_item SET state_id = @removedState WHERE id = @removed", new { removedState = SeedIds.States.Ship.StoryRemoved, removed });
        var comment = await ArrangeCommentAsync(item, "Counted.");
        Guid archivedComment;
        await using (var connection = await OpenDatabaseAsync())
        {
            archivedComment = await new CommentBuilder(item).Archived().InsertAsync(connection);
        }

        var removedComment = await ArrangeCommentAsync(removed, "Hidden.");
        var attachment = await ArrangeAttachmentAsync(item, "Counted text.");
        var imageAttachment = await ArrangeAttachmentAsync(item, null);
        var sprint = await ArrangeSprintAsync("Sprint Delta", "");
        await ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @sprint", new { sprint = await ArrangeSprintAsync("Sprint Eps", "") });

        using var scope = Host.Services.CreateScope();
        var sources = scope.ServiceProvider.GetServices<ISearchTextSource>().ToDictionary(s => s.Kind);
        var comments = await sources[SearchSourceKind.Comment].AllIdsAsync();
        var attachments = await sources[SearchSourceKind.Attachment].AllIdsAsync();
        var sprints = await sources[SearchSourceKind.Sprint].AllIdsAsync();
        var projects = await sources[SearchSourceKind.Project].AllIdsAsync();

        comments.ShouldContain(comment);
        comments.ShouldNotContain(archivedComment);
        comments.ShouldNotContain(removedComment);
        attachments.ShouldContain(attachment);
        attachments.ShouldNotContain(imageAttachment);
        sprints.ShouldContain(sprint);
        projects.ShouldContain(Project);
        comments.ShouldBeUnique();
    }

    private async Task<Guid> ArrangeItemAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(Project).WithTitle(title).InsertAsync(connection);
    }

    private async Task<Guid> ArrangeCommentAsync(Guid item, string body)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new CommentBuilder(item).WithBody(body).InsertAsync(connection);
    }

    private async Task<Guid> ArrangeAttachmentAsync(Guid item, string? searchText)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, search_text) VALUES (@id, @item, @id::text || '.md', 'text/markdown', 3, 'blob/f', @searchText)",
            new { id, item, searchText });
        return id;
    }

    private async Task<Guid> ArrangeSprintAsync(string name, string goal)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, goal) VALUES (@id, @Project, @name, DATE '2090-01-01', DATE '2090-01-14', @goal)",
            new { id, Project, name, goal });
        return id;
    }

    private async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(sql, parameters);
    }

    private async Task<(string Key, string Name)> QueryProjectAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<(string, string)>("SELECT key, name FROM project WHERE id = @Project", new { Project });
    }

    private async Task SyncAsync(SearchSourceKind kind, params Guid[] ids)
    {
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISearchDocWriter>().SyncAsync([.. ids.Select(id => new SearchSource(kind, id))]);
    }

    private async Task<List<Row>> RowsAsync(string kind, Guid sourceId)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<Row>(
            """
            SELECT field, chunk_no AS chunkNo, text, text_hash AS textHash, work_item_id AS workItemId, project_id AS projectId,
                   embedding IS NOT NULL AS embedded
            FROM search_doc WHERE source_kind = @kind AND source_id = @sourceId ORDER BY field, chunk_no
            """,
            new { kind, sourceId })];
    }

    private sealed record Row(string Field, int ChunkNo, string Text, string TextHash, Guid? WorkItemId, Guid ProjectId, bool Embedded);
}

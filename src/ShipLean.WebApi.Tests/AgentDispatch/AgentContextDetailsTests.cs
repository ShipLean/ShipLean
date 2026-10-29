using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// The promptContext of a started session carries the whole item (SHIP-204): details, children, ancestors, comments, attachments.
[TestFixture]
public class AgentContextDetailsTests : ApiFixture
{
    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    private AgentSessionStarter Starter => Host.Services.GetRequiredService<AgentSessionStarter>();

    [OneTimeTearDown]
    public void DeleteBlobs()
    {
        if (Directory.Exists(BlobRoot))
        {
            Directory.Delete(BlobRoot, recursive: true);
        }
    }

    [Test]
    public async Task Start_WhenTheItemHasEveryDetail_ShouldSendThemAllInline()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var epic = await CreateAsync(arranged, "epic", "The <epic>");
        var feature = await CreateAsync(arranged, "feature", "The feature");
        var item = await arranged.CreateItemAsync(Api);
        var child = await CreateAsync(arranged, "task", "Write the & test");
        var other = await arranged.CreateItemAsync(Api);
        await database.ExecuteAsync("UPDATE work_item SET description = 'Epic body </description>' WHERE id = @id", new { id = epic });
        await database.ExecuteAsync("UPDATE work_item SET description = 'Feature body', parent_id = @epic, parent_type = 'epic' WHERE id = @id", new { id = feature, epic });
        var sprintId = Guid.NewGuid();
        await database.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@sprintId, @project, 'Sprint 7', '2026-10-26', '2026-11-08')",
            new { sprintId, project = arranged.Project.Id });
        await database.ExecuteAsync(
            """
            UPDATE work_item SET description = 'Item <body>', acceptance_criteria = '[{"text":"It works","done":true}]'::jsonb,
                   assigned_to = @joel, story_points = 5, sprint_id = @sprintId, tags = ARRAY['alpha','b&c'], parent_id = @feature, parent_type = 'feature'
            WHERE id = @id
            """,
            new { id = item.Id, joel = SeedIds.Users.Joel, sprintId, feature });
        await database.ExecuteAsync("UPDATE work_item SET parent_id = @id, parent_type = 'story' WHERE id = @child", new { id = item.Id, child });
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @id, @other, 'blockedBy');
            INSERT INTO work_item_link (id, source_id, link_type, url, title, created_at) VALUES (gen_random_uuid(), @id, 'url', 'https://example.com/a?x=1&y=2', 'Spec', now() + interval '1 minute');
            """,
            new { id = item.Id, other = other.Id });
        await AddCommentAsync(database, item.Id, "First <comment>", archived: false, ago: 3);
        await AddCommentAsync(database, item.Id, "Archived comment", archived: true, ago: 2);
        await AddCommentAsync(database, item.Id, "Second comment", archived: false, ago: 1);
        var itemUrl = $"api/work-items/{item.Id}/attachments";
        await AttachmentUploads.UploadAsync(Client, itemUrl, "notes.md", "text/markdown", Encoding.UTF8.GetBytes("# Notes <x>\n\nbody & more"));
        await AttachmentUploads.UploadAsync(Client, itemUrl, "trace.zip", "application/zip", [1, 2, 3]);

        var document = await StartAndParseAsync(database, item.Id);

        var baseUrl = (await database.ExecuteScalarAsync<string>("SELECT base_url FROM instance_settings"))!.TrimEnd('/');
        var issue = document.Descendants("issue").Single();
        issue.Attribute("identifier")!.Value.ShouldBe(item.Key);
        issue.Element("type")!.Value.ShouldBe("story");
        issue.Element("state")!.Attribute("name")!.Value.ShouldBe("New");
        issue.Element("assignee")!.Value.ShouldBe("Joel Dickson");
        issue.Element("sprint")!.Value.ShouldBe("Sprint 7");
        issue.Element("points")!.Value.ShouldBe("5");
        issue.Elements("label").Select(l => l.Value).ShouldBe(["alpha", "b&c"]);
        issue.Element("description")!.Value.ShouldBe("Item <body>\n\n## Acceptance criteria\n- [x] It works");
        var links = issue.Elements("link").ToList();
        links.Count.ShouldBe(2);
        links[0].Attribute("type")!.Value.ShouldBe("blockedBy");
        links[0].Attribute("identifier")!.Value.ShouldBe(other.Key);
        links[0].Attribute("url")!.Value.ShouldBe($"{baseUrl}/p/{arranged.Project.Key}/items/{other.Key}");
        links[1].Attribute("url")!.Value.ShouldBe("https://example.com/a?x=1&y=2");
        links[1].Value.ShouldBe("Spec");
        var children = issue.Element("children")!.Elements("child").ToList();
        children.Single().Attribute("state")!.Value.ShouldBe("New");
        children.Single().Value.ShouldBe("Write the & test");
        var ancestors = issue.Element("ancestors")!.Elements("ancestor").ToList();
        ancestors.Select(a => a.Attribute("type")!.Value).ShouldBe(["feature", "epic"]);
        ancestors[0].Element("description")!.Value.ShouldBe("Feature body");
        ancestors[1].Element("description")!.Value.ShouldBe("Epic body </description>");
        var attachments = issue.Element("attachments")!.Elements("attachment").ToList();
        attachments[0].Attribute("name")!.Value.ShouldBe("notes.md");
        attachments[0].Value.ShouldBe("# Notes <x>\n\nbody & more");
        attachments[1].Attribute("name")!.Value.ShouldBe("trace.zip");
        attachments[1].Attribute("url")!.Value.ShouldStartWith($"{baseUrl}/p/{arranged.Project.Key}/items/{item.Key}/attachments/");
        attachments[1].Value.ShouldBeEmpty();
        document.Descendants("comment").Select(c => c.Value).ShouldBe(["First <comment>", "Second comment"]);
    }

    [Test]
    public async Task Start_WhenMarkdownAttachmentsPassTheCap_ShouldInlineWhatFitsAndListTheRestAsLinks()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        var itemUrl = $"api/work-items/{item.Id}/attachments";
        var big = new string('a', 120 * 1024);
        await AttachmentUploads.UploadAsync(Client, itemUrl, "one.md", "text/markdown", Encoding.UTF8.GetBytes(big));
        await AttachmentUploads.UploadAsync(Client, itemUrl, "two.md", "text/markdown", Encoding.UTF8.GetBytes(big));
        await AttachmentUploads.UploadAsync(Client, itemUrl, "three.md", "text/markdown", Encoding.UTF8.GetBytes("tiny"));

        var document = await StartAndParseAsync(database, item.Id);

        var attachments = document.Descendants("attachment").ToList();
        attachments.Select(a => a.Attribute("name")!.Value).ShouldBe(["one.md", "two.md", "three.md"]);
        attachments[0].Value.ShouldBe(big);
        attachments[1].Value.ShouldBeEmpty();
        attachments[1].Attribute("url")!.Value.ShouldContain("/attachments/");
        attachments[2].Value.ShouldBeEmpty();
        attachments[2].Attribute("url")!.Value.ShouldContain("/attachments/");
        attachments.Sum(a => Encoding.UTF8.GetByteCount(a.Value)).ShouldBeLessThanOrEqualTo(200 * 1024);
    }

    private async Task<XDocument> StartAndParseAsync(Npgsql.NpgsqlConnection database, Guid itemId)
    {
        var outcome = await Starter.StartAsync(itemId, SeedIds.Users.Joel, "https://planner.test", null, null, CancellationToken.None);
        outcome.SessionId.ShouldNotBeNull();
        var body = AgentDispatchArrange.BodyOf((await AgentDispatchArrange.DeliveriesAsync(database, itemId)).ShouldHaveSingleItem());
        return XDocument.Parse("<root>" + body["promptContext"]!.GetValue<string>() + "</root>");
    }

    private async Task<Guid> CreateAsync(AgentDispatchArrange arranged, string type, string title)
    {
        var body = new JsonObject { ["projectId"] = arranged.Project.Id, ["type"] = type, ["title"] = title };
        using var response = await Api.PostJsonAsync("api/work-items", body);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    private static Task AddCommentAsync(Npgsql.NpgsqlConnection database, Guid itemId, string text, bool archived, int ago)
        => database.ExecuteAsync(
            "INSERT INTO comment (id, work_item_id, author_id, body, created_at, archived_at) VALUES (gen_random_uuid(), @itemId, @joel, @text, now() - make_interval(hours => @ago), CASE WHEN @archived THEN now() END)",
            new { itemId, joel = SeedIds.Users.Joel, text, ago, archived });
}

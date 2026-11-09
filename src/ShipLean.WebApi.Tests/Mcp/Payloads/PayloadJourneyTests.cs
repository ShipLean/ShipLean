using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The whole path with nothing replaced: an agent asks create_payload_upload for a link, PUTs the bytes to it with a plain
// HttpClient (no cookie, no bearer), and calls tools with `payload`, over the real store and the real PayloadFilter.
[TestFixture]
public class PayloadJourneyTests : McpFixture
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    protected override bool WithProbeTools => false;

    private RecordingUsageEvents Usage => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearUsage() => Usage.Clear();

    [Test]
    public async Task CapturePlan_WhenLargePlanUploaded_ShouldPreviewAndApply()
    {
        var arguments = LargePlan(out var commonBrief, out var planMarkdown);
        var bytes = Encoding.UTF8.GetBytes(arguments.ToJsonString());
        var token = await AgentAsync(ReadWrite);
        var uploaded = await UploadAsync(token, "capture_plan", null, bytes);

        var preview = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{uploaded.Payload}}"}""");
        var applied = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{uploaded.Payload}}","plan":"{{PlanOf(preview)}}"}""");

        bytes.Length.ShouldBeGreaterThan(300 * 1024);
        uploaded.Status.ShouldBe(HttpStatusCode.OK);
        preview.IsToolError().ShouldBeFalse(preview.Body);
        preview.Structured().GetProperty("applied").GetBoolean().ShouldBeFalse();
        preview.Structured().GetProperty("warnings").GetArrayLength().ShouldBe(0);
        preview.Structured().GetProperty("items").GetInt32().ShouldBe(45);
        applied.IsToolError().ShouldBeFalse(applied.Body);
        applied.Structured().GetProperty("applied").GetBoolean().ShouldBeTrue();
        applied.Structured().GetProperty("stories").GetArrayLength().ShouldBe(4);
        var key = applied.Structured().GetProperty("feature").GetString()!;
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM work_item t JOIN work_item s ON s.id = t.parent_id JOIN work_item f ON f.id = s.parent_id WHERE f.key = @key",
            new { key })).ShouldBe(40);
        var ids = (await connection.QueryAsync<Guid>(
            "SELECT a.id FROM attachment a JOIN work_item f ON f.id = a.work_item_id WHERE f.key = @key", new { key })).ToList();
        ids.Count.ShouldBe(2);
        var stored = new List<byte[]>();
        foreach (var id in ids)
        {
            stored.Add(await ReadStoredAsync(id));
        }
        stored.ShouldContain(content => content.SequenceEqual(Encoding.UTF8.GetBytes(commonBrief)), "no attachment holds the common brief's uploaded bytes");
        stored.ShouldContain(content => content.SequenceEqual(Encoding.UTF8.GetBytes(planMarkdown)), "no attachment holds the plan document's uploaded bytes");
    }

    [Test]
    public async Task AttachMarkdown_WhenFileUploadedAsField_ShouldAttachItByteForByte()
    {
        const string text = "# Notes \"quoted\"\r\n\r\nBackslash \\ and C:\\temp\\x, caf\u00e9, \u65e5\u672c\u8a9e, \u2713 \U0001F680\r\nlast line";
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Journey attach target").InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        var expected = Encoding.UTF8.GetBytes(text);
        var token = await AgentAsync(ReadWrite);
        var uploaded = await UploadAsync(token, "attach_markdown", "content", expected);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{key}}","name":"from-file.md","payload":"{{uploaded.Payload}}"}""");

        uploaded.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        var stored = await ReadStoredAsync(reply.Structured().GetProperty("attachment_id").GetGuid());
        stored.ShouldBe(expected);
    }

    [Test]
    public async Task CapturePlan_WhenPayloadUsedByAnotherAgent_ShouldReturnNotFound()
    {
        var owner = await AgentAsync(ReadWrite);
        var other = await AgentAsync(ReadWrite);
        var uploaded = await UploadAsync(owner, "capture_plan", null, Encoding.UTF8.GetBytes(PlanFixture.FeatureEditor.Arguments().ToJsonString()));
        var before = await CountItemsAsync();

        var reply = await CallToolAsync(other, "capture_plan", $$"""{"project":"SHIP","payload":"{{uploaded.Payload}}"}""");
        var ownerReply = await CallToolAsync(owner, "capture_plan", $$"""{"project":"SHIP","payload":"{{uploaded.Payload}}"}""");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue(reply.Body);
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
        error.GetProperty("field").GetString().ShouldBe("payload");
        ownerReply.IsToolError().ShouldBeFalse(ownerReply.Body);
        (await CountItemsAsync()).ShouldBe(before);
    }

    [Test]
    public async Task CapturePlan_WhenUploadedTwiceToSameUrl_ShouldKeepTheFirst()
    {
        var token = await AgentAsync(ReadWrite);
        var asked = await CallToolAsync(token, "create_payload_upload", """{"tool":"capture_plan"}""");
        var payload = asked.Structured().GetProperty("payload").GetString()!;
        var path = new Uri(asked.Structured().GetProperty("upload_url").GetString()!).AbsolutePath;

        using var first = await Client.PutAsync(path, new ByteArrayContent(Encoding.UTF8.GetBytes(TitledPlan("First upload wins"))));
        using var second = await Client.PutAsync(path, new ByteArrayContent(Encoding.UTF8.GetBytes(TitledPlan("Second upload loses"))));
        var preview = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{payload}}"}""");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        preview.IsToolError().ShouldBeFalse(preview.Body);
        preview.Structured().GetProperty("feature").GetString().ShouldBe("First upload wins");
    }

    [Test]
    public async Task BreakDown_WhenSmallChildrenInline_ShouldWorkAsBefore()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Journey inline parent").InsertAsync(connection);
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        var token = await AgentAsync(ReadWrite);

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{key}}","children":[{"title":"Inline child one"},{"title":"Inline child two"}]}""");

        reply.IsToolError().ShouldBeFalse(reply.Body);
        var titles = await connection.QueryAsync<string>("SELECT title FROM work_item WHERE parent_id = @story ORDER BY title", new { story });
        titles.ToArray().ShouldBe(new[] { "Inline child one", "Inline child two" });
    }

    [Test]
    public async Task ToolUsage_WhenCalledByPayload_ShouldTagInputPayload()
    {
        var token = await AgentAsync(ReadWrite);
        var uploaded = await UploadAsync(token, "capture_plan", null, Encoding.UTF8.GetBytes(TitledPlan("Usage by payload")));
        Usage.Clear();

        var preview = await CallToolAsync(token, "capture_plan", $$"""{"project":"SHIP","payload":"{{uploaded.Payload}}"}""");

        preview.IsToolError().ShouldBeFalse(preview.Body);
        var tracked = await Usage.WaitForAsync(e => e.Source == "tool:capture_plan", Wait);
        tracked.Properties[UsageProps.Input].ShouldBe("payload");
    }

    // Four stories of ten tasks, a 60 KB common brief and a 190 KB plan document: inside every plan shape limit, over 300 KB.
    private static JsonObject LargePlan(out string commonBrief, out string planMarkdown)
    {
        var arguments = PlanFixture.FeatureEditor.Arguments();
        arguments.Remove("project");
        commonBrief = Filler("Common brief", 60 * 1024);
        planMarkdown = Filler("Plan document", 190 * 1024);
        arguments["common_brief"] = commonBrief;
        arguments["plan_markdown"] = planMarkdown;
        var stories = new JsonArray();
        for (var s = 1; s <= 4; s++)
        {
            var tasks = new JsonArray();
            for (var t = 1; t <= 10; t++)
            {
                tasks.Add(new JsonObject
                {
                    ["title"] = $"Journey task {s}.{t}",
                    ["brief"] = Filler($"Brief {s}.{t}", 5 * 1024),
                    ["model"] = "strong",
                    ["context_k"] = 45,
                });
            }
            stories.Add(new JsonObject { ["title"] = $"Journey story {s}", ["wave"] = s.ToString(), ["description"] = "A story of the journey.", ["tasks"] = tasks });
        }
        arguments["stories"] = stories;
        return arguments;
    }

    // Markdown lines with quotes and non-ASCII, a little under `bytes` long in UTF-8.
    private static string Filler(string heading, int bytes)
    {
        var text = new StringBuilder($"# {heading}\n");
        while (Encoding.UTF8.GetByteCount(text.ToString()) < bytes - 100)
        {
            text.Append("A line with \"quotes\", a back\\slash, caf\u00e9 and \u2713 to carry the plan.\n");
        }
        return text.ToString();
    }

    private static string TitledPlan(string title)
    {
        var arguments = PlanFixture.FeatureEditor.Arguments();
        arguments.Remove("project");
        arguments["feature"]!["title"] = title;
        return arguments.ToJsonString();
    }

    private static string PlanOf(McpReply preview) => preview.Structured().GetProperty("plan").GetString()!;

    // What the agent does: ask for a link, PUT the bytes to it with a client that has no cookie and no bearer.
    private async Task<Uploaded> UploadAsync(string token, string tool, string? field, byte[] bytes)
    {
        var arguments = field is null ? $$"""{"tool":"{{tool}}"}""" : $$"""{"tool":"{{tool}}","field":"{{field}}"}""";
        var asked = await CallToolAsync(token, "create_payload_upload", arguments);
        var result = asked.Structured();
        using var put = await Client.PutAsync(new Uri(result.GetProperty("upload_url").GetString()!).AbsolutePath, new ByteArrayContent(bytes));
        return new Uploaded(result.GetProperty("payload").GetString()!, put.StatusCode);
    }

    // An agent token of Joel's under a client of its own, so each token is its own authorization.
    private Task<string> AgentAsync(string[] scopes) => TokenAsync(TestUser.Joel, scopes, $"Journey agent {Guid.NewGuid():N}");

    // The bytes the attachment store serves for the download (the blob itself, not the search text kept beside it).
    private async Task<byte[]> ReadStoredAsync(Guid attachmentId)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var (_, blob) = await scope.ServiceProvider.GetRequiredService<AttachmentStore>().OpenAsync(attachmentId);
        await using var content = blob.Content;
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory);
        return memory.ToArray();
    }

    private async Task<int> CountItemsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item");
    }

    private sealed record Uploaded(string Payload, HttpStatusCode Status);
}

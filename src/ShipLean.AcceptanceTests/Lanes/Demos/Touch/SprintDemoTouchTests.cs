using System.Text;
using System.Text.Json.Nodes;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Builders;

namespace ShipLean.AcceptanceTests.Lanes.Demos.Touch;

// The Sprint Demo tab's body on a phone (SHIP-889, L889a): the touch rules are checked on the demo panel only; the sprint header above it is SHIP-724's.
// A ready demo is seeded on Sprint 23 and the Create button is offered on Sprint 22, so the two tests share no sprint.
[TestFixture]
public class SprintDemoTouchTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private static readonly string FixturePath = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4");

    private const string Chapters = """{"durationSeconds":8,"chapters":[{"title":"Introduction","startSeconds":0},{"title":"Checkout v2","startSeconds":4,"featureKey":"SHIP-101"}]}""";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    [CancelAfter(240_000)]
    public async Task ReadyDemo_WhenOpenedOnAPhone_ShouldPlayAndSeekFromAChapter()
    {
        await SeedReadyDemoAsync(SeedIds.Sprints.Sprint23);
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", SeedIds.Sprints.Sprint23);
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready");
        await Expect(tab.Chapters).ToHaveCountAsync(2);
        await Expect(tab.Script).ToBeVisibleAsync();

        (await TouchRules.ViolationsAsync(Page, tab.Panel)).ShouldBeEmpty();

        await tab.Video.EvaluateAsync("v => { v.muted = true; v.preload = 'auto'; }");
        await tab.Chapters.Nth(1).TapAsync();
        await tab.WaitForSeekedAsync(4);
        (await tab.CurrentTimeAsync()).ShouldBeInRange(3.5, 4.5);
        await Expect(tab.Chapters.Nth(1)).ToHaveAttributeAsync("aria-current", "true");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task ClosedSprintWithNoDemo_WhenOpenedOnAPhone_ShouldOfferCreateDemoFullWidth()
    {
        await ArrangeAgentAsync();
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", SeedIds.Sprints.Sprint22);
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "none");
        await Expect(tab.Create).ToBeEnabledAsync();

        (await TouchRules.ViolationsAsync(Page, tab.Panel)).ShouldBeEmpty();
        var band = await tab.StatusBand.BoundingBoxAsync();
        var button = await tab.Create.BoundingBoxAsync();
        ((double)button!.Width).ShouldBeGreaterThan((double)band!.Width - 40);
        ((double)button.Height).ShouldBeGreaterThanOrEqualTo(32);
    }

    // A connection, the project's repository and the instance's address by API: what makes Create demo available on a closed sprint.
    private async Task ArrangeAgentAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = $"Relay {Guid.NewGuid():N}", receiverUrl = "https://relay.example.com/hook" });
        var connectionId = connection!["connection"]!["id"]!.GetValue<Guid>();
        var project = (await SendAsync("GET", $"/api/projects/{SeedIds.Projects.Ship}/settings"))!;
        var general = project["general"]!;
        var put = new JsonObject
        {
            ["general"] = new JsonObject
            {
                ["key"] = general["key"]!.DeepClone(), ["name"] = general["name"]!.DeepClone(), ["description"] = general["description"]!.DeepClone(),
                ["workingDays"] = general["workingDays"]!.DeepClone(), ["holidayCountry"] = general["holidayCountry"]?.DeepClone(),
                ["agentConnectionId"] = connectionId, ["repoUrl"] = "https://github.com/ShipLean/ShipLean.git", ["repoBaseBranch"] = "develop",
            },
            ["members"] = new JsonArray([.. project["members"]!.AsArray().Select(m => (JsonNode)new JsonObject { ["email"] = m!["email"]!.DeepClone(), ["role"] = m["role"]!.DeepClone() })]),
            ["sprints"] = project["sprints"]!.DeepClone(),
            ["states"] = new JsonObject { ["add"] = new JsonArray(), ["update"] = new JsonArray(), ["reorder"] = new JsonArray(), ["remove"] = new JsonArray() },
        };
        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/settings", put);
    }

    // A BAU item is made by ShipLean, never by hand over the API, so the arrangement writes the row the run would have made.
    private async Task SeedReadyDemoAsync(Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var itemId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithTitle("Sprint 23 demo").InsertAsync(connection);
        var video = await AddAttachmentAsync(itemId, "sprint-demo.mp4", "video/mp4", await File.ReadAllBytesAsync(FixturePath));
        var script = await AddAttachmentAsync(itemId, "sprint-demo-script.md", "text/markdown", Encoding.UTF8.GetBytes("## Introduction\n\n**Ann:** Welcome.\n"));
        var chapters = await AddAttachmentAsync(itemId, "sprint-demo-chapters.json", "application/json", Encoding.UTF8.GetBytes(Chapters));
        await using var command = new NpgsqlCommand(
            """
            UPDATE sprint SET demo_work_item_id = @item, demo_video_attachment_id = @video, demo_script_attachment_id = @script, demo_chapters_attachment_id = @chapters,
              demo_duration_seconds = 8, demo_requested_by = @user, demo_requested_via = 'mcp', demo_requested_at = now() - interval '1 hour', demo_delivered_at = now()
            WHERE id = @sprint
            """,
            connection);
        command.Parameters.AddWithValue("item", itemId);
        command.Parameters.AddWithValue("video", video);
        command.Parameters.AddWithValue("script", script);
        command.Parameters.AddWithValue("chapters", chapters);
        command.Parameters.AddWithValue("user", SeedIds.Users.Joel);
        command.Parameters.AddWithValue("sprint", sprintId);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    private async Task<Guid> AddAttachmentAsync(Guid itemId, string name, string contentType, byte[] bytes)
    {
        var response = await Page.APIRequest.PostAsync($"/api/work-items/{itemId}/attachments", new APIRequestContextOptions
        {
            Headers = ClientHeaders,
            Multipart = Page.APIRequest.CreateFormData().Set("file", new FilePayload { Name = name, MimeType = contentType, Buffer = bytes }),
        });
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonNode.Parse(await response.TextAsync())!["id"]!.GetValue<Guid>();
    }

    private async Task<JsonNode?> SendAsync(string method, string path, object? body = null)
    {
        var options = new APIRequestContextOptions { Method = method, Headers = ClientHeaders };
        if (body is not null)
        {
            options.DataObject = body;
        }

        var response = await Page.Context.APIRequest.FetchAsync(path, options);
        var text = await response.TextAsync();
        response.Ok.ShouldBeTrue($"{method} {path} answered {response.Status}: {text}");
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
}

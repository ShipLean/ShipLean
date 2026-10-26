using System.Text.Json;
using ModelContextProtocol.Client;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Feature;

// An agent records a whole Feature over MCP (create, spec twice, a diagram, a code link and its state, a log entry, a
// status) and the Feature page shows each piece. Its own class: the journey writes one Feature of its own.
[TestFixture]
public class FeatureJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string MergeRequest = "https://gitlab.example.com/payments/vault/-/merge_requests/31";

    // A 1x1 PNG.
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Test]
    public async Task FeatureJourney_WhenAgentRecordsWork_ShouldShowEverythingOnTheFeature()
    {
        var token = await Host.IssueAgentToken(TestUser.Joel, new HashSet<string> { "shiplean:read", "shiplean:write" });
        await using var mcp = await McpTestClient.ConnectAsync(new Uri($"{BaseUrl}/mcp"), token);

        var created = await CallAsync(mcp, "create_feature", new()
        {
            ["project"] = Project,
            ["title"] = "Saved cards v2",
            ["goal"] = "Returning customers pay with a saved card in two taps.",
        });
        var key = created.GetProperty("key").GetString()!;
        var itemId = await ItemIdAsync(key);

        // Joel's own entry comes first, so the agent's is the newer one.
        await new WorkItemScreen(Page).OpenAsync(Project, key);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await new DiscussionPanel(Page).PostAsync(itemId, "Kicking this off.");

        await CallAsync(mcp, "attach_markdown", new() { ["feature"] = key, ["name"] = "design.md", ["content"] = "# Design\n\nFirst draft.\n" });
        var design = await CallAsync(mcp, "attach_markdown", new() { ["feature"] = key, ["name"] = "design.md", ["content"] = "# Design\n\nSecond draft, vault tokens only.\n" });
        design.GetProperty("replaced").GetBoolean().ShouldBeTrue();
        await CallAsync(mcp, "attach_file", new() { ["feature"] = key, ["name"] = "diagram.png", ["content_type"] = "image/png", ["base64"] = Png });
        await CallAsync(mcp, "refine_work_item", new() { ["item"] = key, ["description"] = "The flow:\n\n![Flow](diagram.png)\n" });
        await CallAsync(mcp, "link", new() { ["item"] = key, ["url"] = MergeRequest, ["title"] = "Rotate vault keys" });
        await CallAsync(mcp, "report_link_state", new() { ["url"] = MergeRequest, ["state"] = "merged", ["as_of"] = "2026-10-25T09:00:00Z" });
        var comment = await CallAsync(mcp, "comment", new() { ["item"] = key, ["text"] = "Picker merged, vault wiring next." });
        await CallAsync(mcp, "set_status", new() { ["feature"] = key, ["status"] = "Picker merged, vault wiring next." });

        await new WorkItemScreen(Page).OpenAsync(Project, key);
        await new LiveStatus(Page).WaitUntilLiveAsync();

        var band = new StatusBandPanel(Page);
        await Expect(band.StatusLine).ToHaveTextAsync("Picker merged, vault wiring next.");
        await Expect(band.Verified).ToContainTextAsync("Verified just now");
        await Expect(Page.GetByTestId(FeatureTestIds.StatusVerifiedBy)).ToContainTextAsync("through set_status");

        // The attached figure is in the description and its bytes load: a real image, not a broken one.
        var description = Page.GetByTestId(WorkItemTestIds.Description);
        await Expect(description).ToContainTextAsync("diagram.png");
        (await description.EvaluateAsync<string>(
            "el => { const img = el.querySelector('figure[data-md-image=attached] img'); return img && img.complete ? String(img.naturalWidth) : '0'; }"))
            .ShouldBe("1");

        var links = new LinksSectionPanel(Page);
        var card = links.CardTitled("Rotate vault keys");
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card.GetByTestId(FeatureTestIds.LinkCardState)).ToHaveTextAsync("Merged");
        await Expect(card.GetByTestId(FeatureTestIds.LinkCardSource)).ToContainTextAsync("reported by Test Agent");

        var discussion = new DiscussionPanel(Page);
        await Expect(discussion.Comments).ToHaveCountAsync(2);
        await Expect(discussion.Comments.First).ToContainTextAsync("Picker merged, vault wiring next.");
        await Expect(discussion.Comments.First.GetByTestId(WorkItemTestIds.CommentVia)).ToHaveTextAsync("Agent · via MCP");

        var attachment = new AttachmentPage(Page);
        var rail = new AttachmentsRailPanel(Page);
        await rail.Markdown.GetByTestId(FeatureTestIds.RailItem).Filter(new LocatorFilterOptions { HasText = "design.md" }).ClickAsync();
        await Expect(attachment.Name).ToHaveTextAsync("design.md");
        await Expect(attachment.Meta).ToContainTextAsync("replaced once");
        await Expect(attachment.Body).ToContainTextAsync("Second draft, vault tokens only.");
        await Page.GoBackAsync();

        await WorkItemComments.SupersedeAsTomAsync(Page, BaseUrl, comment.GetProperty("comment_id").GetGuid());
        var agentEntry = discussion.Comments.First;
        await Expect(agentEntry.GetByTestId(WorkItemTestIds.CommentSuperseded)).ToHaveCountAsync(1);
        await Expect(agentEntry.GetByTestId(WorkItemTestIds.CommentBody)).ToHaveCSSAsync("text-decoration-line", "line-through");

        var confirmed = await band.ConfirmAsync(itemId);
        confirmed.Ok.ShouldBeTrue();
        await Expect(band.Verified).ToContainTextAsync("Verified just now");
    }

    private async Task<Guid> ItemIdAsync(string key)
    {
        var response = await Page.Context.APIRequest.GetAsync($"{BaseUrl}/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments);
        result.StructuredContent.ShouldNotBeNull();
        result.IsError.ShouldNotBe(true, result.StructuredContent.Value.ToString());
        return result.StructuredContent.Value;
    }
}

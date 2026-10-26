using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.End;

// Sprint 24 ended, with Joel and Tom both on All sprints when Joel applies. Its own class: Apply moves the items.
[TestFixture]
public class SprintEndAppliedTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> Headers = new() { ["X-ShipLean-Client"] = "teammate-context" };

    private async Task<JsonElement> ReadAsync(string path)
    {
        var response = await Page.Context.APIRequest.GetAsync(path);
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    [Test]
    public async Task SprintEnd_WhenApplied_ShouldMoveItemsAndShowOnceForEveryone()
    {
        await EndSprint24Async(ConnectionString);
        var keys = Keys(await ReadRolloverAsync(Page, SeedIds.Sprints.Sprint24));
        keys.Count.ShouldBeGreaterThan(1);
        var toBacklog = keys[0];
        var projectItems = $"/api/projects/{SeedIds.Projects.Ship}/work-items";

        var joel = new SprintEndPrompt(Page);
        await joel.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(joel.Rows).ToHaveCountAsync(keys.Count);

        await using var context = await Page.Context.Browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl });
        var login = await context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email = TestUser.Tom.Email, password = TestUser.Tom.Password },
            Headers = Headers,
        });
        login.Ok.ShouldBeTrue();
        var tomPage = await context.NewPageAsync();
        var tom = new SprintEndPrompt(tomPage);
        await tom.OpenAsync("SHIP");
        await new LiveStatus(tomPage).WaitUntilLiveAsync();
        await Expect(tom.Rows).ToHaveCountAsync(keys.Count);

        await joel.DestinationOf(toBacklog).SelectOptionAsync("backlog");
        var applied = await joel.ApplyAsync(SeedIds.Sprints.Sprint24);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());

        await Expect(joel.Dialog).ToHaveCountAsync(0);
        await Expect(tom.Done).ToBeVisibleAsync();
        await Expect(tom.Apply).ToHaveCountAsync(0);
        await tom.DoneClose.ClickAsync();
        await Expect(tom.Dialog).ToHaveCountAsync(0);
        await tom.OpenAsync("SHIP");
        await Expect(tom.Dialog).ToHaveCountAsync(0);
        await Expect(tom.Banner).ToHaveCountAsync(0);
        await joel.OpenAsync("SHIP");
        await Expect(joel.Dialog).ToHaveCountAsync(0);

        var moved = new Dictionary<string, string?>();
        foreach (var key in keys)
        {
            var item = await ReadAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
            var sprint = item.GetProperty("sprint");
            moved[key] = sprint.ValueKind == JsonValueKind.Null ? null : sprint.GetProperty("id").GetString();
        }

        moved[toBacklog].ShouldBeNull();
        keys.Skip(1).ShouldAllBe(key => moved[key] == SeedIds.Sprints.Sprint25.ToString());

        // Open tasks went with their story; closed tasks stayed in the ended sprint.
        var tasks = (await ReadAsync(projectItems)).EnumerateArray()
            .Where(i => i.GetProperty("type").GetProperty("id").GetString() == "task" && i.GetProperty("parent").ValueKind != JsonValueKind.Null)
            .Where(i => keys.Contains(i.GetProperty("parent").GetProperty("key").GetString()))
            .ToList();
        var closed = tasks.Where(t => t.GetProperty("state").GetProperty("category").GetString() == "completed").ToList();
        var open = tasks.Where(t => t.GetProperty("state").GetProperty("category").GetString() != "completed").ToList();
        closed.ShouldNotBeEmpty();
        open.ShouldNotBeEmpty();
        closed.ShouldAllBe(t => t.GetProperty("sprint").GetProperty("id").GetString() == SeedIds.Sprints.Sprint24.ToString());
        open.Where(t => moved[t.GetProperty("parent").GetProperty("key").GetString()!] == null)
            .ShouldAllBe(t => t.GetProperty("sprint").ValueKind == JsonValueKind.Null);
        open.Where(t => moved[t.GetProperty("parent").GetProperty("key").GetString()!] != null)
            .ShouldAllBe(t => t.GetProperty("sprint").GetProperty("id").GetString() == SeedIds.Sprints.Sprint25.ToString());

        var after = await ReadRolloverAsync(Page, SeedIds.Sprints.Sprint24);
        after.GetProperty("status").GetString().ShouldBe("decided");
    }
}

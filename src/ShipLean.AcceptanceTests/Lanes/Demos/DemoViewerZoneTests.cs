using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.Contracts;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// The Demo tab dates the request and the upload link in the viewer's zone. The user is in Asia/Bangkok, the server is not:
// the report was "Requested today at 00:02" when the viewer's clock read 07:02.
[TestFixture]
public class DemoViewerZoneTests : BangkokViewerTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone at the DayAt calls in SprintDemoReader; the tab then reads the UTC time.
    [Test]
    [CancelAfter(240_000)]
    public async Task DemoTab_WhenViewerIsInBangkok_ShouldShowRequestAndExpiryInBangkokTime()
    {
        await ArrangeAgentAsync();
        var sprint = SeedIds.Sprints.Sprint23;
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", sprint);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var before = TimeProvider.System.GetUtcNow();
        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        var after = TimeProvider.System.GetUtcNow();

        // The request was made between before and after, and the link lasts 24 hours, so its expiry is "tomorrow" at the same
        // minute. The tab shows those instants in Bangkok (UTC+07:00); the minutes are formatted here, not by DisplayDate, and
        // no instant is read from the database. A UTC tab would read seven hours earlier.
        var bangkokMinutes = new List<string>();
        var utcMinutes = new List<string>();
        for (var at = before; at < after.AddMinutes(1); at = at.AddMinutes(1))
        {
            bangkokMinutes.Add(at.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            utcMinutes.Add(at.ToOffset(TimeSpan.Zero).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
        }

        bangkokMinutes.ShouldNotBe(utcMinutes);
        var minutes = string.Join('|', bangkokMinutes.Distinct());
        await Expect(tab.StatusBand).ToContainTextAsync(new Regex($"Requested today at ({minutes})"));
        await Expect(tab.StatusBand).ToContainTextAsync(new Regex($"Link valid until tomorrow at ({minutes})"));
    }

    private async Task ArrangeAgentAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = $"Relay {Guid.NewGuid():N}", receiverUrl = "https://relay.example.com/hook" });
        var connectionId = connection!["connection"]!["id"]!.GetValue<Guid>();

        var instance = (await SendAsync("GET", "/api/instance/settings"))!;
        instance["baseUrl"] = BaseUrl;
        await SendAsync("PUT", "/api/instance/settings", instance);

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
        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", new { automatic = false, environment = (string?)null });
    }

    private async Task<JsonNode?> SendAsync(string method, string path, object? body = null)
    {
        var options = new Microsoft.Playwright.APIRequestContextOptions { Method = method, Headers = ClientHeaders };
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

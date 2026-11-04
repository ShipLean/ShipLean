using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// The demo made by hand, with the Automatic setting off: closing the sprint starts nothing, the Demo tab offers Create demo, and the click starts the session.
// A retry (Remake demo on a Ready tab) reuses the BAU item, kills the old upload link, and the new upload replaces all three files together.
// Its own class: closing Sprint 24 moves its items. The retry test uses Sprint 23, which is already closed, so the two tests share no sprint and each filter runs alone.
[TestFixture]
public class CreateDemoJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private static readonly string FixturePath = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4");

    private const string FirstScript = "## Introduction\n\n**Ann:** First cut of the Sprint 24 demo.\n";

    private const string SecondScript = "## Introduction\n\n**Ann:** Second cut of the Sprint 24 demo.\n\n## Checkout v2\n\n**Keith:** Checkout is now one step.\n";

    [Test]
    [CancelAfter(240_000)]
    public async Task CloseSprint_WhenAutomaticOff_ShouldOfferCreateDemo()
    {
        var callbackToken = await ArrangeAgentAsync();
        await EndSprint24Async(ConnectionString);
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await prompt.PressCloseAsync();
        var applied = await prompt.ApplyAsync(SeedIds.Sprints.Sprint24);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        // With Automatic off the close starts nothing: the tab offers Create demo, and no BAU item or session exists yet.
        var tab = new SprintDemoTab(Page);
        await tab.Tab.ClickAsync();
        await tab.Panel.WaitForAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "none");
        await Expect(tab.Create).ToBeEnabledAsync();
        await Expect(tab.Create).ToHaveTextAsync("Create demo");
        (await ReadScalarAsync<bool>($"SELECT demo_work_item_id IS NULL AND demo_requested_at IS NULL FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'")).ShouldBeTrue();

        // The click makes the BAU item and starts the session; the open tab turns Working with no reload.
        var create = await Page.RunAndWaitForResponseAsync(
            tab.CreateDemoAsync,
            r => r.Request.Method == "POST" && r.Url.EndsWith($"/api/sprints/{SeedIds.Sprints.Sprint24}/demo", StringComparison.Ordinal));
        create.Ok.ShouldBeTrue(await create.TextAsync());
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        await Expect(tab.ItemLink).ToHaveCountAsync(1);

        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'");
        (await ReadScalarAsync<string>($"SELECT demo_requested_via FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'")).ShouldBe("button");
        (await ReadScalarAsync<string>($"SELECT key FROM work_item WHERE id = '{itemId}'")).ShouldBe(await tab.ItemLink.InnerTextAsync());
        (await ReadScalarAsync<long>($"SELECT count(*) FROM agent_session WHERE work_item_id = '{itemId}'")).ShouldBe(1);

        // The session's first delivery carries a usable upload link, and the agent's upload makes the demo.
        var uploadUrl = UploadUrlIn(await ReadScalarAsync<string>(
            $"SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = '{itemId}' ORDER BY d.created_at, d.seq LIMIT 1"));
        uploadUrl.ShouldStartWith(BaseUrl + "/api/uploads/demo?token=");
        var fixture = await File.ReadAllBytesAsync(FixturePath);
        using var http = new HttpClient();
        using var response = await http.PostAsync(uploadUrl, Form(Chapters(2), FirstScript, fixture));
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        await RespondAsync(callbackToken, await ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}'"));
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready", new() { Timeout = 30_000 });
        await Expect(tab.Script).ToContainTextAsync("First cut of the Sprint 24 demo.");
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task CreateDemo_WhenRetried_ShouldReuseTheItem()
    {
        var callbackToken = await ArrangeAgentAsync();
        var sprint = SeedIds.Sprints.Sprint23;
        var tab = new SprintDemoTab(Page);
        await tab.GotoAsync("SHIP", sprint);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "none");
        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        var itemKey = await tab.ItemLink.InnerTextAsync();
        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{sprint}'");

        // The first demo: its upload, then the agent's final response, which ends the session and turns the tab Ready.
        var firstUrl = await LatestUploadUrlAsync(itemId);
        var fixture = await File.ReadAllBytesAsync(FixturePath);
        using var http = new HttpClient();
        using (var first = await http.PostAsync(firstUrl, Form(Chapters(2), FirstScript, fixture)))
        {
            first.IsSuccessStatusCode.ShouldBeTrue(await first.Content.ReadAsStringAsync());
        }

        await RespondAsync(callbackToken, await LatestSessionAsync(itemId));
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready", new() { Timeout = 30_000 });
        var firstFiles = await ListLiveAsync(itemId);
        firstFiles.Select(a => a.Name).Order().ShouldBe(["sprint-demo-chapters.json", "sprint-demo-script.md", "sprint-demo.mp4"]);
        var firstPointers = await ReadPointersAsync(sprint);
        firstPointers.ShouldBe(PointersOf(firstFiles));

        // The retry: Remake demo on the Ready tab starts a second session on the same item and kills the first link.
        await Expect(tab.Create).ToHaveTextAsync("Remake demo");
        await tab.CreateDemoAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        await Expect(tab.ItemLink).ToHaveTextAsync(itemKey);
        (await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{sprint}'")).ShouldBe(itemId);
        (await ReadScalarAsync<long>($"SELECT count(*) FROM agent_session WHERE work_item_id = '{itemId}'")).ShouldBe(2);
        (await ReadScalarAsync<long>("SELECT count(*) FROM work_item WHERE type = 'bau' AND title = 'Sprint 23 demo'")).ShouldBe(1);

        var secondUrl = await LatestUploadUrlAsync(itemId);
        secondUrl.ShouldNotBe(firstUrl);
        using (var stale = await http.PostAsync(firstUrl, Form(Chapters(2), "## Stale\n\nThis must not land.\n", fixture)))
        {
            stale.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the first upload link is dead after the retry");
        }

        (await ListLiveAsync(itemId)).ShouldBe(firstFiles, customMessage: "a refused upload on the dead link changes nothing");
        (await ReadPointersAsync(sprint)).ShouldBe(firstPointers);
        (await ReadScalarAsync<long>($"SELECT count(*) FROM transfer_token WHERE work_item_id = '{itemId}' AND kind = 'demo' AND revoked_at IS NULL")).ShouldBe(1);

        // The second upload: a different video (the fixture plus a trailing, valid free box), script and chapters, replacing all three files together.
        byte[] second = [.. fixture, 0, 0, 0, 8, (byte)'f', (byte)'r', (byte)'e', (byte)'e'];
        using (var accepted = await http.PostAsync(secondUrl, Form(Chapters(3), SecondScript, second)))
        {
            accepted.IsSuccessStatusCode.ShouldBeTrue(await accepted.Content.ReadAsStringAsync());
        }

        var secondFiles = await ListLiveAsync(itemId);
        secondFiles.Select(a => a.Name).Order().ToList().ShouldBe(["sprint-demo-chapters.json", "sprint-demo-script.md", "sprint-demo.mp4"], customMessage: "only the new set is live");
        foreach (var old in firstFiles)
        {
            secondFiles.Select(a => a.Id).ShouldNotContain(old.Id, $"{old.Name} was replaced, not kept");
        }

        var secondPointers = await ReadPointersAsync(sprint);
        secondPointers.ShouldBe(PointersOf(secondFiles));
        var stored = await Page.APIRequest.GetAsync($"/api/attachments/{secondPointers.Video}/content");
        stored.Ok.ShouldBeTrue();
        (await stored.BodyAsync()).ShouldBe(second);
        (await ReadScalarAsync<long>($"SELECT count(*) FROM attachment WHERE work_item_id = '{itemId}' AND archived_at IS NULL")).ShouldBe(3);

        // The open tab shows the new demo: the second script and the three chapters.
        await RespondAsync(callbackToken, await LatestSessionAsync(itemId));
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready", new() { Timeout = 30_000 });
        await Expect(tab.ItemLink).ToHaveTextAsync(itemKey);
        await Expect(tab.Script).ToContainTextAsync("Second cut of the Sprint 24 demo.");
        await Expect(tab.Script).Not.ToContainTextAsync("First cut");
        await Expect(tab.Chapters).ToHaveCountAsync(3);
    }

    // A connection, the project's repository, the instance's address (this host, so the upload link reaches it) and the Demos setting off, all by API.
    private async Task<string> ArrangeAgentAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = $"Relay {Guid.NewGuid():N}", receiverUrl = "https://relay.example.com/hook" });
        var connectionId = connection!["connection"]!["id"]!.GetValue<Guid>();
        var callbackToken = connection["callbackToken"]!.GetValue<string>();

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
        return callbackToken;
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

    // The agent's final response, through the connection's own callback token on the Linear-compatible endpoint: nothing else here plays the agent.
    private async Task RespondAsync(string callbackToken, Guid sessionId)
    {
        const string Mutation = "mutation createAgentActivity($input: AgentActivityCreateInput!) { agentActivityCreate(input: $input) { success } }";
        var body = new JsonObject
        {
            ["query"] = Mutation,
            ["variables"] = new JsonObject { ["input"] = new JsonObject { ["agentSessionId"] = sessionId, ["content"] = new JsonObject { ["type"] = "response", ["body"] = "The demo is uploaded." } } },
        };
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/linear/graphql") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {callbackToken}");
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        JsonNode.Parse(text)!["data"]!["agentActivityCreate"]!["success"]!.GetValue<bool>().ShouldBeTrue(text);
    }

    private Task<Guid> LatestSessionAsync(Guid itemId) =>
        ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}' ORDER BY created_at DESC LIMIT 1");

    // The upload link in the newest session's first delivery.
    private async Task<string> LatestUploadUrlAsync(Guid itemId) =>
        UploadUrlIn(await ReadScalarAsync<string>(
            $"""
            SELECT d.body::text FROM agent_delivery d WHERE d.session_id = (SELECT id FROM agent_session WHERE work_item_id = '{itemId}' ORDER BY created_at DESC LIMIT 1)
            ORDER BY d.created_at, d.seq LIMIT 1
            """));

    private async Task<List<(Guid Id, string Name)>> ListLiveAsync(Guid itemId)
    {
        var listed = (await SendAsync("GET", $"/api/work-items/{itemId}/attachments"))!.AsArray();
        return [.. listed.Select(a => (a!["id"]!.GetValue<Guid>(), a["name"]!.GetValue<string>())).OrderBy(a => a.Item2, StringComparer.Ordinal)];
    }

    private static (Guid? Video, Guid? Script, Guid? Chapters) PointersOf(List<(Guid Id, string Name)> files) =>
        (files.Single(a => a.Name == "sprint-demo.mp4").Id, files.Single(a => a.Name == "sprint-demo-script.md").Id, files.Single(a => a.Name == "sprint-demo-chapters.json").Id);

    private async Task<(Guid? Video, Guid? Script, Guid? Chapters)> ReadPointersAsync(Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT demo_video_attachment_id, demo_script_attachment_id, demo_chapters_attachment_id FROM sprint WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", sprintId);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return (reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2));
    }

    private async Task<T> ReadScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"No row for: {sql}"));
    }

    // The upload link inside the delivered body: the first string in the JSON that carries /api/uploads/demo?token=.
    private static string UploadUrlIn(string deliveryBody)
    {
        using var document = JsonDocument.Parse(deliveryBody);
        var pattern = new System.Text.RegularExpressions.Regex(@"https?://[^\s""'<>)\]]+/api/uploads/demo\?token=[A-Za-z0-9_%.~-]+");
        foreach (var text in Strings(document.RootElement))
        {
            if (pattern.Match(text) is { Success: true } match)
            {
                return match.Value;
            }
        }

        throw new InvalidOperationException("The delivered body carries no upload link.");
    }

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
        _ => [],
    };

    // The chapters part: the fixture is 8 seconds long, and every chapter after the first names SHIP-101.
    private static string Chapters(int count)
    {
        var chapters = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            var chapter = new JsonObject { ["title"] = i == 0 ? "Introduction" : $"Chapter {i + 1}", ["startSeconds"] = i * 2 };
            if (i > 0)
            {
                chapter["featureKey"] = "SHIP-101";
            }

            chapters.Add(chapter);
        }

        return new JsonObject { ["durationSeconds"] = 8, ["chapters"] = chapters }.ToJsonString();
    }

    private static MultipartFormDataContent Form(string chapters, string script, byte[] video) => new()
    {
        { new ByteArrayContent(Encoding.UTF8.GetBytes(chapters)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }, "chapters", "chapters.json" },
        { new ByteArrayContent(Encoding.UTF8.GetBytes(script)) { Headers = { ContentType = new MediaTypeHeaderValue("text/markdown") } }, "script", "script.md" },
        { new ByteArrayContent(video) { Headers = { ContentType = new MediaTypeHeaderValue("video/mp4") } }, "video", "demo.mp4" },
    };
}

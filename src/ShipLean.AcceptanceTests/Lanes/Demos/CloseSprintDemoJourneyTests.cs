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
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TestIds;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// The whole demo path on the real app, browser and upload: an ended sprint is closed, the automatic run starts a session and renders its prompt, the agent's
// upload (read from the delivered body, since no relay runs here) lands through the real endpoint, and the open Demo tab turns Ready by the realtime event,
// plays and seeks. Its own class: closing Sprint 24 moves its items. The layout test uses Sprint 23, which is already closed, so each filter runs alone.
[TestFixture]
public class CloseSprintDemoJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private static readonly string FixturePath = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4");

    private const string Script = "## Introduction\n\n**Ann:** Welcome to the Sprint 24 demo.\n\n## Checkout v2\n\n**Keith:** Checkout now takes one step.\n";

    [Test]
    [CancelAfter(240_000)]
    public async Task CloseSprint_WhenAutomaticOn_ShouldHandAgentThePromptAndPlayTheDemo()
    {
        var callbackToken = await ArrangeAgentAsync(automatic: true);
        await EndSprint24Async(ConnectionString);
        var tab = new SprintDemoTab(Page);
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await prompt.PressCloseAsync();
        var applied = await prompt.ApplyAsync(SeedIds.Sprints.Sprint24);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        // The automatic run answers the close. The tab is opened from the sprint's own page, on the connection that is already live, so no event of the run
        // can fall between the tab's first read and its subscription (a fresh page load would miss one sent before its connection joined the project).
        await tab.Tab.ClickAsync();
        await tab.Panel.WaitForAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });
        await Expect(tab.Video).ToHaveCountAsync(0);

        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'");
        var uploadUrl = UploadUrlIn(await ReadScalarAsync<string>(
            $"SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = '{itemId}' ORDER BY d.created_at, d.seq LIMIT 1"));
        uploadUrl.ShouldStartWith(BaseUrl + "/api/uploads/demo?token=");

        var fixture = await File.ReadAllBytesAsync(FixturePath);
        using var http = new HttpClient();
        using var response = await http.PostAsync(uploadUrl, Form(Chapters(2), Script, fixture));
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());

        // The three files are live on the item and the sprint's pointers name them; the video's bytes are the fixture's.
        var live = await ListAttachmentsAsync(itemId);
        live.Select(a => a.Name).Order().ShouldBe(["sprint-demo-chapters.json", "sprint-demo-script.md", "sprint-demo.mp4"]);
        var pointers = await ReadPointersAsync(SeedIds.Sprints.Sprint24);
        pointers.Video.ShouldBe(live.Single(a => a.Name == "sprint-demo.mp4").Id);
        pointers.Script.ShouldBe(live.Single(a => a.Name == "sprint-demo-script.md").Id);
        pointers.Chapters.ShouldBe(live.Single(a => a.Name == "sprint-demo-chapters.json").Id);
        var stored = await Page.APIRequest.GetAsync($"/api/attachments/{pointers.Video}/content");
        stored.Ok.ShouldBeTrue();
        (await stored.BodyAsync()).ShouldBe(fixture);

        // Recorded from before the tab can load the video, so the browser's own first request is seen and not one its HTTP cache answers.
        var mediaPath = $"/api/attachments/{pointers.Video}/content";
        var media = new List<(int Status, string Range)>();
        Page.Response += (_, r) =>
        {
            if (new Uri(r.Url).AbsolutePath == mediaPath)
            {
                media.Add((r.Status, r.Request.Headers.GetValueOrDefault("range") ?? string.Empty));
            }
        };

        // The upload alone leaves the agent's session live, and a live session reads as Working. The agent's last act is its final response, through the
        // connection's own callback token on the Linear-compatible endpoint: nothing else here plays the agent.
        var sessionId = await ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}'");
        await RespondAsync(callbackToken, sessionId);

        // The open tab turns Ready by the realtime event, with no reload.
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready", new() { Timeout = 30_000 });
        await Expect(tab.Video).ToBeVisibleAsync();
        await Expect(tab.Chapters).ToHaveCountAsync(2);
        await Expect(tab.Features).ToHaveCountAsync(1);
        await Expect(tab.Features.First).ToContainTextAsync("SHIP-101");
        await Expect(tab.Script).ToContainTextAsync("Welcome to the Sprint 24 demo.");
        await Expect(tab.Script).ToContainTextAsync("Checkout now takes one step.");

        await tab.Video.EvaluateAsync("v => { v.muted = true; v.preload = 'auto'; }");
        await WaitForVideoAsync("v.readyState >= 3");
        (await tab.Video.EvaluateAsync<double>("v => v.seekable.length > 0 ? v.seekable.end(0) : 0")).ShouldBeInRange(7.0, 9.0);

        await tab.Video.EvaluateAsync("v => v.play()");
        await WaitForVideoAsync("v.currentTime > 0.3");
        await tab.Video.EvaluateAsync("v => v.pause()");

        await tab.Video.EvaluateAsync("v => { v.currentTime = 6; }");
        await WaitForVideoAsync("!v.seeking && Math.abs(v.currentTime - 6) < 0.5");
        (await tab.CurrentTimeAsync()).ShouldBeInRange(5.5, 6.5);

        await tab.SeekAsync(1);
        await WaitForVideoAsync("!v.seeking && Math.abs(v.currentTime - 4) < 0.5");
        (await tab.CurrentTimeAsync()).ShouldBeInRange(3.5, 4.5);
        await tab.SeekAsync(0);
        await WaitForVideoAsync("!v.seeking && v.currentTime < 0.5");

        media.ShouldNotBeEmpty();
        media[0].Status.ShouldBe(206, "the browser's first request for the video is a ranged one, answered 206");
        media[0].Range.ShouldStartWith("bytes=");
        media.ShouldAllBe(m => m.Status == 206);

        // A fresh request with no browser cache in front of it: bytes 2 to 5 come back as a 206 naming where they sit.
        var ranged = await Page.APIRequest.GetAsync(mediaPath, new() { Headers = new Dictionary<string, string> { ["Range"] = "bytes=2-5" } });
        ranged.Status.ShouldBe(206);
        ranged.Headers["content-range"].ShouldBe($"bytes 2-5/{fixture.Length}");
        (await ranged.BodyAsync()).ShouldBe(fixture[2..6]);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task ReadyDemo_WhenFortyChapters_ShouldKeepItsBarsInTheWindow()
    {
        _ = await ArrangeAgentAsync(automatic: false);
        var itemId = await CreateBauItemAsync();
        var fixture = await File.ReadAllBytesAsync(FixturePath);
        var video = await AddAttachmentAsync(itemId, "sprint-demo.mp4", "video/mp4", fixture);
        var script = await AddAttachmentAsync(itemId, "sprint-demo-script.md", "text/markdown", Encoding.UTF8.GetBytes(Script));
        var chapters = await AddAttachmentAsync(itemId, "sprint-demo-chapters.json", "application/json", Encoding.UTF8.GetBytes(Chapters(40)));
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
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
            command.Parameters.AddWithValue("sprint", SeedIds.Sprints.Sprint23);
            (await command.ExecuteNonQueryAsync()).ShouldBe(1);
        }

        var tab = new SprintDemoTab(Page);
        foreach (var width in new[] { 1280, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 800);
            await tab.GotoAsync("SHIP", SeedIds.Sprints.Sprint23);
            await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready");
            await Expect(tab.Chapters).ToHaveCountAsync(40);

            var layout = await Page.EvaluateAsync<JsonElement>(
                """
                ([band, list, video]) => {
                  const rect = (id) => document.querySelector(`[data-testid="${id}"]`).getBoundingClientRect();
                  const chapters = document.querySelector(`[data-testid="${list}"]`);
                  const root = document.documentElement;
                  return {
                    bandBottom: rect(band).bottom, bandRight: rect(band).right, videoTop: rect(video).top,
                    listScrolls: chapters.scrollHeight > chapters.clientHeight, listBottom: rect(list).bottom,
                    innerHeight: window.innerHeight, innerWidth: window.innerWidth, scrollWidth: root.scrollWidth, clientWidth: root.clientWidth,
                  };
                }
                """,
                new[] { "demo-status-band", "demo-chapters", "demo-video" });

            layout.GetProperty("listScrolls").GetBoolean().ShouldBeTrue($"40 chapters scroll inside their list at {width}");
            layout.GetProperty("listBottom").GetDouble().ShouldBeLessThanOrEqualTo(layout.GetProperty("innerHeight").GetDouble(), $"the chapter list stays in the window at {width}");
            layout.GetProperty("bandBottom").GetDouble().ShouldBeLessThanOrEqualTo(layout.GetProperty("innerHeight").GetDouble());
            layout.GetProperty("bandRight").GetDouble().ShouldBeLessThanOrEqualTo(layout.GetProperty("innerWidth").GetDouble());
            layout.GetProperty("scrollWidth").GetInt32().ShouldBe(layout.GetProperty("clientWidth").GetInt32(), $"no horizontal overflow at {width}");
            await Expect(tab.Create).ToBeInViewportAsync();
        }
    }

    // Waits, bounded, for a media condition on the Demo tab's video (the page object's own seek wait has no bound, so a seek that never happens would hang the run).
    private Task WaitForVideoAsync(string condition) =>
        Page.WaitForFunctionAsync($"id => {{ const v = document.querySelector(`[data-testid=\"${{id}}\"]`); return v !== null && ({condition}); }}", DemosTestIds.DemoVideo, new() { Timeout = 15_000 });

    // A connection, the project's repository, the instance's address (this host, so the upload link reaches it) and the Demos setting, all by API.
    private async Task<string> ArrangeAgentAsync(bool automatic)
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

        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", new { automatic, environment = (string?)null });
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

    // A BAU item is made by ShipLean, never by hand over the API, so the arrangement writes the row the run would have made.
    private async Task<Guid> CreateBauItemAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithTitle("Sprint 23 demo").InsertAsync(connection);
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

    private async Task<List<(Guid Id, string Name)>> ListAttachmentsAsync(Guid itemId)
    {
        var listed = (await SendAsync("GET", $"/api/work-items/{itemId}/attachments"))!.AsArray();
        return [.. listed.Select(a => (a!["id"]!.GetValue<Guid>(), a["name"]!.GetValue<string>()))];
    }

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

    // The chapters part: the fixture is 8 seconds long. Two chapters name SHIP-101 on the second; more are spread evenly.
    private static string Chapters(int count)
    {
        var chapters = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            chapters.Add(count == 2 ? Named(i) : new JsonObject { ["title"] = $"Chapter {i + 1}", ["startSeconds"] = i * 0.2 });
        }

        return new JsonObject { ["durationSeconds"] = 8, ["chapters"] = chapters }.ToJsonString();
    }

    private static JsonObject Named(int index)
    {
        var chapter = new JsonObject { ["title"] = index == 0 ? "Introduction" : "Checkout v2", ["startSeconds"] = index * 4 };
        if (index > 0)
        {
            chapter["featureKey"] = "SHIP-101";
        }

        return chapter;
    }

    private static MultipartFormDataContent Form(string chapters, string script, byte[] video)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(chapters)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }, "chapters", "chapters.json" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(script)) { Headers = { ContentType = new MediaTypeHeaderValue("text/markdown") } }, "script", "script.md" },
            { new ByteArrayContent(video) { Headers = { ContentType = new MediaTypeHeaderValue("video/mp4") } }, "video", "demo.mp4" },
        };
        return form;
    }
}

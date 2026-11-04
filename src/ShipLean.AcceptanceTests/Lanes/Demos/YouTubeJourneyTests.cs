using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.Application.Fakes.YouTube;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// YouTube on the real page, with the FakeYouTubeClient behind IYouTubeClient and no request to Google: the consent page is a host the test answers itself, and the
// embed's host is refused. The first test is the check refusing a channel that gives another visibility, and the person choosing what came back. The second is a
// forward that fails and is retried until the Demo tab renders the embed. The fake shows what ShipLean does when YouTube answers as it is told to; it does not
// show that Google answers so. Each test uses its own sprint (the second closes Sprint 24), so each filter runs alone.
[TestFixture]
public class YouTubeJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private static readonly string FixturePath = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DemoFixtures", "demo-fixture.mp4");

    private const string Script = "## Introduction\n\n**Ann:** Welcome to the Sprint 24 demo.\n";

    private const string Chapters = "{\"durationSeconds\":8,\"chapters\":[{\"title\":\"Introduction\",\"startSeconds\":0},{\"title\":\"Checkout v2\",\"startSeconds\":4,\"featureKey\":\"SHIP-101\"}]}";

    // The fake's authorization URL points here: not Google, and the test answers it.
    private const string ConsentHost = "accounts.example.test";

    private readonly FakeYouTubeClient _fake = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IYouTubeClient>();
        services.AddSingleton<IYouTubeClient>(_fake);
        services.RemoveAll<IYouTubeSettings>();
        services.AddSingleton<IYouTubeSettings>(new OurOAuthClient());
    };

    [SetUp]
    public void ResetTheFake()
    {
        _fake.ForcedVisibility = null;
        _fake.FailWith(null);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Connect_WhenChannelForcesPrivate_ShouldRefuseThenPrivateConnects()
    {
        _fake.ForcedVisibility = YouTubeVisibility.Private;
        var tab = new DemoSettingsTab(Page);
        await tab.GotoAsync("SHIP");
        await Expect(tab.YouTubeConnect).ToBeVisibleAsync();

        // Connect sends the browser to the consent page. That host is the test's own: it answers with a stub page, and what the browser asked for is read.
        await Page.RouteAsync(
            url => new Uri(url).Host == ConsentHost,
            route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/html", Body = "<p>consent</p>" }));
        var consentRequest = await Page.RunAndWaitForRequestAsync(() => tab.ConnectAsync(), request => new Uri(request.Url).Host == ConsentHost);
        var consentUrl = new Uri(consentRequest.Url);
        var state = HttpUtility.ParseQueryString(consentUrl.Query)["state"];
        state.ShouldNotBeNullOrWhiteSpace();
        consentUrl.AbsoluteUri.ShouldNotContain("client-secret");

        // The consent comes back as Google would send the browser: the callback with a code and the state the URL carried. It stores a pending channel and
        // lands on the Demos tab, where nothing is connected yet.
        await Page.GotoAsync($"{BaseUrl}/api/youtube/callback?code=consent-code&state={Uri.EscapeDataString(state)}");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/settings/demos\?youtube=pending$"));
        await tab.Panel.WaitForAsync();
        await Expect(tab.YouTubeChannel).ToHaveTextAsync("Fake channel");
        await Expect(tab.YouTubeConnected).ToHaveCountAsync(0);
        await Expect(tab.YouTubeCheck).ToBeDisabledAsync();

        // Unlisted is chosen and the channel gives private. The check answers pending with the reason, and the page shows it with nothing connected.
        await ChooseAsync(tab, "Unlisted");
        var refused = await CheckAsync(tab);
        refused["status"]!.GetValue<string>().ShouldBe("pending");
        refused["refusal"]!.GetValue<string>().ShouldContain("gave the test video private visibility, not the unlisted you chose");
        await Expect(tab.YouTubeRefusal).ToContainTextAsync("private visibility, not the unlisted you chose");
        await Expect(tab.YouTubeConnected).ToHaveCountAsync(0);
        (await ReadChannelAsync()).ShouldBe(("pending", null));

        // The test video is always taken down, and no video was left on the channel.
        _fake.Uploads.ShouldHaveSingleItem().Upload.Visibility.ShouldBe(YouTubeVisibility.Unlisted);
        _fake.Deletes.ShouldBe([_fake.Uploads[0].VideoId]);

        // Choosing what came back checks again and connects. The refusal goes.
        await ChooseAsync(tab, "Private");
        var connected = await CheckAsync(tab);
        connected["status"]!.GetValue<string>().ShouldBe("connected");
        connected["visibility"]!.GetValue<string>().ShouldBe("private");
        connected["refusal"].ShouldBeNull();
        await Expect(tab.YouTubeConnected).ToBeVisibleAsync();
        await Expect(tab.YouTubeRefusal).ToHaveCountAsync(0);
        (await ReadChannelAsync()).ShouldBe(("connected", "private"));
        _fake.Uploads.Count.ShouldBe(2);
        _fake.Uploads[1].Upload.Visibility.ShouldBe(YouTubeVisibility.Private);
        _fake.Deletes.ShouldBe([.. _fake.Uploads.Select(u => u.VideoId)]);

        // It is what the server keeps, not only what this page remembers.
        await tab.GotoAsync("SHIP");
        await Expect(tab.YouTubeConnected).ToBeVisibleAsync();
        await Expect(tab.YouTubeRefusal).ToHaveCountAsync(0);
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Demo_WhenForwardFailsThenRetried_ShouldPlayAttachmentThenRenderTheEmbed()
    {
        // The embed's host is refused for the whole test, and every request the page makes to a YouTube host is recorded: nothing leaves the host.
        var refusedHosts = new List<string>();
        await Page.RouteAsync(
            url => new Uri(url).Host.Contains("youtube", StringComparison.OrdinalIgnoreCase),
            route =>
            {
                lock (refusedHosts)
                {
                    refusedHosts.Add(route.Request.Url);
                }

                return route.AbortAsync();
            });

        var callbackToken = await ArrangeAgentAsync();
        await ConnectUnlistedChannelAsync();

        // The sprint is closed and the agent's demo uploaded: with a channel connected, the delivery leaves the sprint waiting to be forwarded.
        await EndSprint24Async(ConnectionString);
        var tab = new SprintDemoTab(Page);
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await prompt.PressCloseAsync();
        var applied = await prompt.ApplyAsync(SeedIds.Sprints.Sprint24);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());
        await Expect(prompt.Dialog).ToHaveCountAsync(0);
        await tab.Tab.ClickAsync();
        await tab.Panel.WaitForAsync();
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });

        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'");
        var uploadUrl = UploadUrlIn(await ReadScalarAsync<string>(
            $"SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = '{itemId}' ORDER BY d.created_at, d.seq LIMIT 1"));
        using (var http = new HttpClient())
        {
            using var response = await http.PostAsync(uploadUrl, Form(await File.ReadAllBytesAsync(FixturePath)));
            response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        }

        var sessionId = await ReadScalarAsync<Guid>($"SELECT id FROM agent_session WHERE work_item_id = '{itemId}'");
        await RespondAsync(callbackToken, sessionId);
        await Expect(tab.StatusBand).ToHaveAttributeAsync("data-status", "ready", new() { Timeout = 30_000 });
        (await ReadYouTubeAsync()).State.ShouldBe("pending");
        await Expect(tab.YouTubeNotice).ToContainTextAsync("Sending to YouTube");
        await Expect(tab.Video).ToBeVisibleAsync();

        // YouTube's quota is used up: the forward fails, nothing is on the channel, and the tab says why while the attachment plays.
        _fake.FailWith(YouTubeFailureKind.Quota);
        (await PublishDueAsync()).ShouldBe(0);
        var failed = await ReadYouTubeAsync();
        failed.State.ShouldBe("failed");
        failed.Failure.ShouldNotBeNull().ShouldContain("daily upload quota");
        _fake.Uploads.Count.ShouldBe(1, "Only the connect step's test video was uploaded.");
        await Expect(tab.YouTubeNotice).ToContainTextAsync("YouTube upload failed");
        await Expect(tab.YouTubeNotice).ToContainTextAsync("daily upload quota");
        await Expect(tab.YouTubeRetry).ToBeVisibleAsync();
        await Expect(tab.Video).ToBeVisibleAsync();
        await Expect(tab.Embed).ToHaveCountAsync(0);

        // Retry puts it back to waiting; the next pass sends it unlisted, as the channel was connected.
        _fake.FailWith(null);
        var retried = await Page.RunAndWaitForResponseAsync(
            () => tab.RetryYouTubeAsync(),
            response => response.Request.Method == "POST" && response.Url.EndsWith("/youtube/retry", StringComparison.Ordinal));
        retried.Status.ShouldBe(200, await retried.TextAsync());
        (await ReadYouTubeAsync()).State.ShouldBe("pending");
        await Expect(tab.YouTubeNotice).ToContainTextAsync("Sending to YouTube");
        await Expect(tab.YouTubeRetry).ToHaveCountAsync(0);

        (await PublishDueAsync()).ShouldBe(1);
        var published = await ReadYouTubeAsync();
        published.State.ShouldBe("published");
        published.Privacy.ShouldBe("unlisted");
        published.VideoId.ShouldNotBeNull();
        // The connect step's own test video was the first upload and was deleted; the demo is the second.
        _fake.Uploads.Count.ShouldBe(2);
        var sent = _fake.Uploads[1];
        sent.VideoId.ShouldBe(published.VideoId);
        sent.Upload.Visibility.ShouldBe(YouTubeVisibility.Unlisted);
        sent.Upload.Title.ShouldEndWith(": Sprint 24 demo");
        sent.Upload.Description.ShouldContain("0:04 Checkout v2");
        sent.Content.ToArray().ShouldBe(await File.ReadAllBytesAsync(FixturePath));

        // Published unlisted: the tab swaps the file for YouTube's embed. Only the iframe's attributes are read, because the host is refused.
        await Expect(tab.Embed).ToBeVisibleAsync();
        await Expect(tab.Embed).ToHaveAttributeAsync("src", $"https://www.youtube-nocookie.com/embed/{published.VideoId}");
        await Expect(tab.Embed).ToHaveAttributeAsync("referrerpolicy", "strict-origin-when-cross-origin");
        await Expect(tab.Embed).ToHaveAttributeAsync("allowfullscreen", new Regex(".*"));
        await Expect(tab.Video).ToHaveCountAsync(0);
        await Expect(tab.YouTubeNotice).ToHaveCountAsync(0);
        await Expect(tab.WatchOnYouTube).ToBeVisibleAsync();
        await Expect(tab.PrivateNote).ToHaveCountAsync(0);

        // The same on a fresh load, so it is what the server says and not only what the open tab held.
        await tab.GotoAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(tab.Embed).ToHaveAttributeAsync("src", $"https://www.youtube-nocookie.com/embed/{published.VideoId}");
        await Expect(tab.Video).ToHaveCountAsync(0);
        lock (refusedHosts)
        {
            refusedHosts.ShouldAllBe(url => url.StartsWith("https://www.youtube-nocookie.com/embed/", StringComparison.Ordinal));
        }
    }

    // The radios have no test id each, and AG0046 refuses GetByLabel, GetByRole and Locator in this project. The group's own id finds the control; the label whose whole
    // text is the visibility's name is clicked inside it, the way a person clicks it, and the radio it names must read checked afterwards (a disabled radio would not).
    private static async Task ChooseAsync(DemoSettingsTab tab, string label)
    {
        var chosen = await tab.YouTubeVisibility.EvaluateAsync<bool>(
            "(group, name) => { const label = [...group.querySelectorAll('label')].find(l => l.textContent.trim() === name); label.click(); return label.control.checked; }",
            label);
        chosen.ShouldBeTrue($"{label} is checked after choosing it.");
    }

    // Presses Check and returns the body of the answer that does the work.
    private static async Task<JsonNode> CheckAsync(DemoSettingsTab tab)
    {
        var response = await tab.Panel.Page.RunAndWaitForResponseAsync(
            () => tab.CheckAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/youtube/check", StringComparison.Ordinal));
        response.Status.ShouldBe(200, await response.TextAsync());
        return JsonNode.Parse(await response.TextAsync())!;
    }

    private async Task<(string Status, string? Visibility)> ReadChannelAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT status, visibility FROM project_youtube_channel WHERE project_id = @project AND archived_at IS NULL", connection);
        command.Parameters.AddWithValue("project", SeedIds.Projects.Ship);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue("The project has a live channel row.");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private async Task<(string State, string? VideoId, string? Privacy, string? Failure)> ReadYouTubeAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT demo_youtube_state, demo_youtube_video_id, demo_youtube_privacy, demo_youtube_failure FROM sprint WHERE id = @sprint", connection);
        command.Parameters.AddWithValue("sprint", SeedIds.Sprints.Sprint24);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    // The publisher's own pass, called as the 30-second loop would (the loop is not hosted here), so the test decides when the forward runs.
    private Task<int> PublishDueAsync() => ActivatorUtilities.CreateInstance<DemoYouTubePublisher>(Host.Services).PublishDueAsync();

    // The channel is connected through the routes the page uses: start, the callback the consent would send the browser to, and the check at unlisted.
    private async Task ConnectUnlistedChannelAsync()
    {
        var started = await SendAsync("POST", $"/api/projects/{SeedIds.Projects.Ship}/youtube/start");
        var state = HttpUtility.ParseQueryString(new Uri(started!["url"]!.GetValue<string>()).Query)["state"]!;
        var callback = await Page.Context.APIRequest.GetAsync($"/api/youtube/callback?code=consent-code&state={Uri.EscapeDataString(state)}", new APIRequestContextOptions { MaxRedirects = 0 });
        callback.Status.ShouldBe(302, await callback.TextAsync());
        callback.Headers["location"].ShouldEndWith("youtube=pending");
        var checkedChannel = await SendAsync("POST", $"/api/projects/{SeedIds.Projects.Ship}/youtube/check", new { visibility = "unlisted" });
        checkedChannel!["status"]!.GetValue<string>().ShouldBe("connected");
        (await ReadChannelAsync()).ShouldBe(("connected", "unlisted"));
    }

    // A connection, the project's repository, the instance's address (this host, so the upload link reaches it) and Automatic on, all by API.
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

        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", new { automatic = true, environment = (string?)null });
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

    // The agent's last act: its final response through the connection's own callback token on the Linear-compatible endpoint.
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
        var pattern = new Regex(@"https?://[^\s""'<>)\]]+/api/uploads/demo\?token=[A-Za-z0-9_%.~-]+");
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

    private static MultipartFormDataContent Form(byte[] video) => new()
    {
        { new ByteArrayContent(Encoding.UTF8.GetBytes(Chapters)) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } }, "chapters", "chapters.json" },
        { new ByteArrayContent(Encoding.UTF8.GetBytes(Script)) { Headers = { ContentType = new MediaTypeHeaderValue("text/markdown") } }, "script", "script.md" },
        { new ByteArrayContent(video) { Headers = { ContentType = new MediaTypeHeaderValue("video/mp4") } }, "video", "demo.mp4" },
    };

    // The instance's OAuth client for YouTube: set, so the panel offers Connect. The fake never opens the secret.
    private sealed class OurOAuthClient : IYouTubeSettings
    {
        public Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<YouTubeClientSettings?>(new YouTubeClientSettings("journey-client.apps.example", "journey-client-secret"));
    }
}

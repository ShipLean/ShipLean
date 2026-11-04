using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Demos;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Demos;

// A project's Demos settings on the real page. The first test is the agent check gating Automatic: the box is disabled and says why until the project has a
// connection and a repository, and the server refuses it too. The second is the brand: a logo and tokens set through the panel reach the prompt an agent is
// handed on close, the link serves the stored bytes as an attachment, and what the project wrote that the prompt must not carry (a script, a name outside the
// seven, a refused line) is not in it. Its own class: the second closes Sprint 24, and each filter runs alone.
[TestFixture]
public class DemoSettingsJourneyTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeaders = new() { [ClientHeader.Name] = "acceptance-test" };

    private const string Repository = "https://github.com/ShipLean/ShipLean.git";

    // A 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly byte[] DirtySvg = Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\" viewBox=\"0 0 40 40\"><script>alert('j3-script')</script>"
        + "<rect width=\"40\" height=\"40\" fill=\"#c0392b\" onclick=\"alert('j3-click')\"/><circle cx=\"20\" cy=\"20\" r=\"8\" fill=\"#ffffff\"/></svg>");

    private const string ValidTokens = "--color-accent: #c0392b;\n--color-bg: #101820;\n--brand-flair: #123456;";

    [Test]
    [CancelAfter(240_000)]
    public async Task Automatic_WhenNoAgent_ShouldBeDisabledUntilConnectionAndRepositoryAreSet()
    {
        var tab = new DemoSettingsTab(Page);
        await tab.GotoAsync("SHIP");

        // No connection and no repository: the box is off and disabled, and the panel says which is missing and where to fix it.
        await Expect(tab.Automatic).ToBeDisabledAsync();
        await Expect(tab.Automatic).Not.ToBeCheckedAsync();
        await Expect(tab.AutomaticBlocked).ToContainTextAsync("no agent connection");
        await Expect(tab.AutomaticBlocked).ToContainTextAsync("General settings");

        // The server refuses it as well, with the same sentence, so the disabled box is not the only gate.
        var refused = await Page.Context.APIRequest.FetchAsync($"/api/projects/{SeedIds.Projects.Ship}/demo-settings",
            new APIRequestContextOptions { Method = "PUT", Headers = ClientHeaders, DataObject = new { automatic = true, environment = (string?)null } });
        refused.Status.ShouldBe(400, await refused.TextAsync());
        var refusal = await refused.TextAsync();
        refusal.ShouldContain("no agent connection");
        refusal.ShouldContain("automatic");

        // A connection alone is not enough: the repository is the next thing missing.
        var connectionId = await CreateConnectionAsync();
        await SetAgentAsync(connectionId, repoUrl: null);
        await tab.GotoAsync("SHIP");
        await Expect(tab.Automatic).ToBeDisabledAsync();
        await Expect(tab.AutomaticBlocked).ToContainTextAsync("no repository");

        // Both set: the check passes, the explanation goes, and Automatic can be turned on and saved.
        await SetAgentAsync(connectionId, Repository);
        await tab.GotoAsync("SHIP");
        await Expect(tab.Automatic).ToBeEnabledAsync();
        await Expect(tab.AutomaticBlocked).ToHaveCountAsync(0);
        await tab.Automatic.CheckAsync();
        var saved = await Page.RunAndWaitForResponseAsync(
            () => tab.Save.ClickAsync(),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/demo-settings", StringComparison.Ordinal));
        saved.Status.ShouldBe(200, await saved.TextAsync());
        (await SendAsync("GET", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings"))!["automatic"]!.GetValue<bool>().ShouldBeTrue();

        await tab.GotoAsync("SHIP");
        await Expect(tab.Automatic).ToBeCheckedAsync();
        await Expect(tab.Automatic).ToBeEnabledAsync();
    }

    [Test]
    [CancelAfter(240_000)]
    public async Task Brand_WhenLogoAndTokensSet_ShouldReachTheDeliveredPrompt()
    {
        await ArrangeAgentAsync();
        var tab = new DemoSettingsTab(Page);
        await tab.GotoAsync("SHIP");

        // Nothing set: ShipLean's logo, marked as the default.
        await Expect(tab.LogoDefault).ToBeVisibleAsync();

        // A PNG: the server stores it, the preview shows that logo and no longer says Default.
        var uploaded = await Page.RunAndWaitForResponseAsync(
            () => tab.UploadLogoAsync("mark.png", "image/png", Png),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/demo-brand/logo", StringComparison.Ordinal));
        uploaded.Status.ShouldBe(200, await uploaded.TextAsync());
        await Expect(tab.LogoDefault).ToHaveCountAsync(0);
        await Expect(tab.LogoPreview).ToHaveAttributeAsync("src", new Regex(@"/demo-brand/logo\?v="));
        await WaitForPreviewToLoadAsync(tab);
        var pngSrc = (await tab.LogoPreview.GetAttributeAsync("src"))!;
        var shownPng = await Page.APIRequest.GetAsync(pngSrc);
        shownPng.Ok.ShouldBeTrue();
        (await shownPng.BodyAsync()).ShouldBe(Png);

        // An SVG with a script and an event attribute, uploaded over the PNG: cleaned, stored, previewed.
        uploaded = await Page.RunAndWaitForResponseAsync(
            () => tab.UploadLogoAsync("mark.svg", "image/svg+xml", DirtySvg),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/demo-brand/logo", StringComparison.Ordinal));
        uploaded.Status.ShouldBe(200, await uploaded.TextAsync());
        await Expect(tab.LogoPreview).Not.ToHaveAttributeAsync("src", pngSrc);
        await WaitForPreviewToLoadAsync(tab);
        var svgSrc = (await tab.LogoPreview.GetAttributeAsync("src"))!;
        var shownSvg = await Page.APIRequest.GetAsync(svgSrc);
        shownSvg.Ok.ShouldBeTrue();
        shownSvg.Headers["content-type"].ShouldStartWith("image/svg+xml");
        var shownSvgText = Encoding.UTF8.GetString(await shownSvg.BodyAsync());
        shownSvgText.ShouldContain("#c0392b");
        shownSvgText.ShouldNotContain("<script", Case.Insensitive);
        shownSvgText.ShouldNotContain("j3-script");
        shownSvgText.ShouldNotContain("onclick", Case.Insensitive);

        // Tokens: a valid set, with a name outside the seven. It is stored as written and listed in the panel.
        var tokensSaved = await Page.RunAndWaitForResponseAsync(
            () => tab.SaveTokensAsync(ValidTokens),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/demo-brand/tokens", StringComparison.Ordinal));
        tokensSaved.Status.ShouldBe(200, await tokensSaved.TextAsync());
        await Expect(tab.TokensError).ToHaveCountAsync(0);
        await Expect(tab.Tokens).ToHaveValueAsync(ValidTokens);

        // A refused set names its line, and what was stored stays.
        var refusedTokens = await Page.RunAndWaitForResponseAsync(
            () => tab.SaveTokensAsync("--color-bg: #101820;\n--color-accent: url(https://evil.example/x.png);"),
            response => response.Request.Method == "PUT" && response.Url.EndsWith("/demo-brand/tokens", StringComparison.Ordinal));
        refusedTokens.Status.ShouldBe(400, await refusedTokens.TextAsync());
        await Expect(tab.TokensError).ToContainTextAsync("Line 2");
        (await SendAsync("GET", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings"))!["brand"]!["tokens"]!.GetValue<string>().ShouldBe(ValidTokens);

        // Close the sprint with Automatic on: the run hands an agent its prompt.
        await EndSprint24Async(ConnectionString);
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await prompt.PressCloseAsync();
        var applied = await prompt.ApplyAsync(SeedIds.Sprints.Sprint24);
        applied.Ok.ShouldBeTrue(await applied.TextAsync());
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        // The automatic run answers the close: the Demo tab reads Working once its session is started, and the delivery is written with it.
        var demo = new SprintDemoTab(Page);
        await demo.Tab.ClickAsync();
        await demo.Panel.WaitForAsync();
        await Expect(demo.StatusBand).ToHaveAttributeAsync("data-status", "working", new() { Timeout = 30_000 });

        var itemId = await ReadScalarAsync<Guid>($"SELECT demo_work_item_id FROM sprint WHERE id = '{SeedIds.Sprints.Sprint24}'");
        var body = await ReadScalarAsync<string>(
            $"SELECT d.body::text FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = '{itemId}' ORDER BY d.created_at, d.seq LIMIT 1");
        var text = string.Join('\n', Strings(JsonDocument.Parse(body).RootElement));

        // The prompt carries the logo as a link, never inline, and the project's seven-name tokens with their values.
        var logoLink = Regex.Match(text, @"https?://[^\s""'<>)\]]+/api/downloads/demo-logo\?token=[A-Za-z0-9_%.~-]+");
        logoLink.Success.ShouldBeTrue("The delivered prompt holds the logo link.");
        logoLink.Value.ShouldStartWith(BaseUrl + "/api/downloads/demo-logo?token=");
        text.ShouldContain("--color-accent: #c0392b;");
        text.ShouldContain("--color-bg: #101820;");

        // A name outside the seven is stored and never delivered, and neither is the refused line or anything the logo's SVG carried.
        text.ShouldNotContain("--brand-flair");
        text.ShouldNotContain("#123456");
        text.ShouldNotContain("evil.example");
        text.ShouldNotContain("j3-script");

        // The agent's link gives the stored bytes as an attachment, with the script already gone.
        using var http = new HttpClient();
        using var download = await http.GetAsync(logoLink.Value);
        download.IsSuccessStatusCode.ShouldBeTrue(await download.Content.ReadAsStringAsync());
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        download.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        download.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        var downloaded = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());
        downloaded.ShouldBe(shownSvgText);
        downloaded.ShouldContain("#c0392b");
        downloaded.ShouldNotContain("<script", Case.Insensitive);
        downloaded.ShouldNotContain("j3-script");
        downloaded.ShouldNotContain("onclick", Case.Insensitive);
    }

    // The preview is an img: it is loaded when the browser decoded something, so a refused or broken file would leave a width of 0.
    private static Task WaitForPreviewToLoadAsync(DemoSettingsTab tab) =>
        tab.LogoPreview.EvaluateAsync("img => img.complete && img.naturalWidth > 0 ? null : new Promise((resolve, reject) => { img.addEventListener('load', resolve, { once: true }); img.addEventListener('error', () => reject(new Error('The logo preview did not load.')), { once: true }); })");

    private async Task<Guid> CreateConnectionAsync()
    {
        var connection = await SendAsync("POST", "/api/instance/agent-connections", new { name = $"Relay {Guid.NewGuid():N}", receiverUrl = "https://relay.example.com/hook" });
        return connection!["connection"]!["id"]!.GetValue<Guid>();
    }

    // A connection, the project's repository, the instance's address (this host, so the logo link reaches it) and Automatic on, all by API.
    private async Task ArrangeAgentAsync()
    {
        var instance = (await SendAsync("GET", "/api/instance/settings"))!;
        instance["baseUrl"] = BaseUrl;
        await SendAsync("PUT", "/api/instance/settings", instance);

        await SetAgentAsync(await CreateConnectionAsync(), Repository);
        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/demo-settings", new { automatic = true, environment = (string?)null });
    }

    private async Task SetAgentAsync(Guid? connectionId, string? repoUrl)
    {
        var project = (await SendAsync("GET", $"/api/projects/{SeedIds.Projects.Ship}/settings"))!;
        var general = project["general"]!;
        var put = new JsonObject
        {
            ["general"] = new JsonObject
            {
                ["key"] = general["key"]!.DeepClone(), ["name"] = general["name"]!.DeepClone(), ["description"] = general["description"]!.DeepClone(),
                ["workingDays"] = general["workingDays"]!.DeepClone(), ["holidayCountry"] = general["holidayCountry"]?.DeepClone(),
                ["agentConnectionId"] = connectionId, ["repoUrl"] = repoUrl, ["repoBaseBranch"] = repoUrl is null ? null : "develop",
            },
            ["members"] = new JsonArray([.. project["members"]!.AsArray().Select(m => (JsonNode)new JsonObject { ["email"] = m!["email"]!.DeepClone(), ["role"] = m["role"]!.DeepClone() })]),
            ["sprints"] = project["sprints"]!.DeepClone(),
            ["states"] = new JsonObject { ["add"] = new JsonArray(), ["update"] = new JsonArray(), ["reorder"] = new JsonArray(), ["remove"] = new JsonArray() },
        };
        await SendAsync("PUT", $"/api/projects/{SeedIds.Projects.Ship}/settings", put);
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

    private async Task<T> ReadScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException($"No row for: {sql}"));
    }

    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
        _ => [],
    };
}

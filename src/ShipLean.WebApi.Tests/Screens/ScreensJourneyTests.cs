using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Screens;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Screens;

// The screens' server paths as the phone drives them: a device bearer for the person, /mcp as the agent. The person is a fresh
// member of SHIP, so the seed's own Features, digest marks and retro points never reach them. The Feature is made through
// create_feature and everything after it through a route or tool.
[TestFixture]
[CancelAfter(120000)]
public class ScreensJourneyTests : McpFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    protected override bool WithProbeTools => false;

    // Only a bearer signs anyone in, as on the phone.
    protected override bool UseTestSignIn => false;

    private async Task<Guid> FreshMemberAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await NeedsYouArrangement.MemberAsync(connection);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private static async Task NoContentAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<FeaturesOverviewDto> OverviewAsync(ApiClient phone)
    {
        using var response = await phone.GetAsync($"api/projects/{Ship}/features/overview");
        return await ReadAsync<FeaturesOverviewDto>(response);
    }

    private static async Task<FeatureDigestDto> DigestAsync(ApiClient phone)
    {
        using var response = await phone.GetAsync($"api/projects/{Ship}/features/digest");
        return await ReadAsync<FeatureDigestDto>(response);
    }

    private static FeatureRowDto ActiveRow(FeaturesOverviewDto overview, string key)
        => overview.Groups.Single(g => g.Label == "Active").Rows.Single(r => r.Key == key);

    // Creates a Feature as the person through MCP and returns its id and key.
    private async Task<(Guid Id, string Key)> CreateFeatureAsync(string token, string title)
    {
        var reply = await CallToolAsync(token, "create_feature", $$"""{"project":"SHIP","title":"{{title}}","goal":"Prove the screens."}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var key = reply.Structured().GetProperty("key").GetString()!;
        await using var connection = await OpenDatabaseAsync();
        return (await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key }), key);
    }

    [Test]
    public async Task HealthJourney_WhenAgentSetsAtRiskThroughMcp_ShouldShowInOverviewAndDigest()
    {
        var user = await FreshMemberAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, user);
        var app = new ApiClient(phone.Client);
        var agent = await TokenAsync(TestUser.Of(user), ReadWrite);
        var feature = await CreateFeatureAsync(agent, "Health journey");

        var reply = await CallToolAsync(agent, "set_status",
            $$"""{"feature":"{{feature.Key}}","status":"Vendor slipped","state":"Active","health":"atRisk"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());

        var overview = await OverviewAsync(app);
        overview.ChangedSince.ShouldBeGreaterThan(0);
        ActiveRow(overview, feature.Key).Health.ShouldBe(FeatureHealth.AtRisk);

        var digest = await DigestAsync(app);
        var line = digest.Changed.Single(i => i.Id == feature.Id).Lines.Single(l => l.Kind == DigestLineKind.Health);
        line.AgentName.ShouldBe("Test Agent (unverified)");

        await NoContentAsync(app.PostJsonAsync($"api/projects/{Ship}/features/digest/seen"));

        (await OverviewAsync(app)).ChangedSince.ShouldBe(0);
        (await DigestAsync(app)).Changed.ShouldNotContain(i => i.Id == feature.Id);
    }

    [Test]
    public async Task OverviewJourney_WhenStoryHasOpenQuestion_ShouldCountItOnFeatureWithOnChildren()
    {
        var user = await FreshMemberAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, user);
        var app = new ApiClient(phone.Client);
        var agent = await TokenAsync(TestUser.Of(user), ReadWrite);
        var feature = await CreateFeatureAsync(agent, "Needs you journey");
        var set = await CallToolAsync(agent, "set_status", $$"""{"feature":"{{feature.Key}}","status":"Going","state":"Active"}""");
        set.IsToolError().ShouldBeFalse(set.ResultText());
        var made = await CallToolAsync(agent, "break_down", $$"""{"parent":"{{feature.Key}}","children":[{"title":"Needs you story"}]}""");
        made.IsToolError().ShouldBeFalse(made.ResultText());
        var storyKey = made.Structured().GetProperty("items")[0].GetProperty("key").GetString()!;

        ActiveRow(await OverviewAsync(app), feature.Key).NeedsYou.ShouldBeNull();

        var ask = await CallToolAsync(agent, "ask", $$"""{"item":"{{storyKey}}","text":"Which card?"}""");
        ask.IsToolError().ShouldBeFalse(ask.ResultText());

        ActiveRow(await OverviewAsync(app), feature.Key).NeedsYou.ShouldBe(new NeedsYouDto(1, 0, true));
    }

    [Test]
    public async Task SupportJourney_WhenPointSupportedTwiceThenWithdrawn_ShouldShowOnceThenNone()
    {
        var user = await FreshMemberAsync();
        using var phone = await DeviceBearerClient.CreateAsync(Host, user);
        var app = new ApiClient(phone.Client);
        var sprint = SeedIds.Sprints.Sprint24;
        var text = $"Journey point {Guid.NewGuid():N}";
        // Not RetroPointCreate: ApiClient writes enums as numbers and the API reads only the camel-case name.
        using var created = await app.PostJsonAsync($"api/projects/{Ship}/retros/points",
            new { sprintId = sprint, category = "improve", text });
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var pointId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var url = $"api/retros/points/{pointId}/support";

        await NoContentAsync(app.PostJsonAsync(url));
        await NoContentAsync(app.PostJsonAsync(url));

        var supported = await PointAsync(app, sprint, pointId);
        supported.Supporters.Select(u => u.Id).ShouldBe([user]);
        supported.SupportedByMe.ShouldBeTrue();

        await NoContentAsync(app.DeleteAsync(url));

        var withdrawn = await PointAsync(app, sprint, pointId);
        withdrawn.Supporters.ShouldBeEmpty();
        withdrawn.SupportedByMe.ShouldBeFalse();
    }

    private static async Task<RetroPointDto> PointAsync(ApiClient app, Guid sprint, Guid pointId)
    {
        using var response = await app.GetAsync($"api/sprints/{sprint}/retro");
        var retro = await ReadAsync<SprintRetroDto>(response);
        return retro.Cards.SelectMany(c => c.Points).Single(p => p.Id == pointId);
    }
}

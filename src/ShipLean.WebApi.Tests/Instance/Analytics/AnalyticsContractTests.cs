using System.Net;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.Analytics;

// The analytics routes against the in-memory stub: the wire shapes are pinned; the stub's values are not asserted.
[TestFixture]
public class AnalyticsContractTests : ApiFixture
{
    private static JsonNode Pinned(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))!;

    private static void ShouldHaveShapeOf(JsonNode actual, JsonNode pinned)
        => actual.AsObject().Select(p => p.Key).Order().ShouldBe(pinned.AsObject().Select(p => p.Key).Order());

    [Test]
    public async Task Get_WhenAdmin_ShouldMatchThePinnedShape()
    {
        using var response = await Api.GetAsync("api/instance/analytics");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var body = JsonNode.Parse(text)!;
        ShouldHaveShapeOf(body, Pinned("analytics-settings.json"));
        body["provider"]!.GetValue<string>().ShouldBe("none");
    }

    [Test]
    public async Task Put_WhenAdmin_ShouldAnswerThePinnedShape()
    {
        using var response = await Api.PutJsonAsync("api/instance/analytics", new { provider = "umami", host = "https://umami.example.com", siteKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71", sendNames = false });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        ShouldHaveShapeOf(JsonNode.Parse(text)!, Pinned("analytics-settings.json"));
    }

    [Test]
    public async Task Get_WhenNotAdmin_ShouldBeForbidden()
    {
        using var response = await Api.ActingAs(TestUser.Mei).GetAsync("api/instance/analytics");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task Test_WhenAdmin_ShouldMatchThePinnedResultShape()
    {
        using var response = await Api.PostJsonAsync("api/instance/analytics/test", new { });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var body = JsonNode.Parse(text)!;
        ShouldHaveShapeOf(body, Pinned("analytics-test-result.json"));
        body["ok"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public async Task Migration_WhenApplied_ShouldDefaultProviderToNone()
    {
        await using var connection = await OpenDatabaseAsync();

        var row = await Dapper.SqlMapper.QuerySingleAsync<(string Provider, string? Host, string? SiteKey, bool SendNames)>(
            connection, "SELECT analytics_provider AS Provider, analytics_host AS Host, analytics_site_key AS SiteKey, analytics_send_names AS SendNames FROM instance_settings");

        row.ShouldBe(("none", null, null, false));
    }
}

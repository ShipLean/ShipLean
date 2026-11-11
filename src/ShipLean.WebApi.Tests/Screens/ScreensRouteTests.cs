using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Retros.Api;

namespace ShipLean.WebApi.Tests.Screens;

// The phone screens' routes through the real app: the Features overview and digest answer in their final shapes for a member,
// the writes answer 204 (support and withdraw twice each, as they are idempotent), and anyone else gets not_found. Nothing here
// depends on what the reads hold, except the overview's groups, whose order and folded Closed group are the contract.
[TestFixture]
public class ScreensRouteTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    [OneTimeSetUp]
    public async Task InsertRetroPoint()
    {
        await using var connection = await OpenDatabaseAsync();
        await RetroSweepData.InsertAsync(connection);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    [Test]
    public async Task FeaturesOverviewRoute_WhenStubbed_ShouldReturnFinalShape()
    {
        using var response = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{Ship}/features/overview");
        var overview = await ReadAsync<FeaturesOverviewDto>(response);

        overview.ProjectId.ShouldBe(Ship);
        overview.SinceLabel.ShouldStartWith("since ");
        string[] order = ["Active", "Resolved", "New", "Closed"];
        overview.Groups.Select(g => g.Label).ShouldBe(order.Where(label => overview.Groups.Any(g => g.Label == label)), ignoreOrder: false);
        overview.Groups.ShouldAllBe(g => g.Folded == (g.Label == "Closed"));
        overview.Groups.Where(g => g.Folded).ShouldAllBe(g => g.Rows.Count == 0);
    }

    [TestCase("")]
    public async Task FeatureDigestRoutes_WhenStubbed_ShouldAnswerInFinalShape(string query)
    {
        using var getResponse = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{Ship}/features/digest{query}");
        var digest = await ReadAsync<FeatureDigestDto>(getResponse);
        using var seenResponse = await Api.ActingAs(TestUser.Tom).PostJsonAsync($"api/projects/{Ship}/features/digest/seen");

        digest.SinceLabel.ShouldStartWith("since ");
        digest.NeedsYou.ShouldNotBeNull();
        digest.Changed.ShouldNotBeNull();
        seenResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task RetroSupportRoutes_WhenStubbed_ShouldReturnNoContent()
    {
        var url = $"api/retros/points/{RetroSweepData.Point}/support";
        var api = Api.ActingAs(TestUser.Mei);
        var statuses = new List<HttpStatusCode>();

        foreach (var send in new Func<Task<HttpResponseMessage>>[] { () => api.PostJsonAsync(url), () => api.PostJsonAsync(url), () => api.DeleteAsync(url), () => api.DeleteAsync(url) })
        {
            using var response = await send();
            statuses.Add(response.StatusCode);
        }

        statuses.ShouldAllBe(s => s == HttpStatusCode.NoContent);
    }

    [TestCase("GET", "features/overview")]
    [TestCase("GET", "features/digest")]
    [TestCase("POST", "features/digest/seen")]
    public async Task ScreensRoutes_WhenNotAMember_ShouldReturnNotFound(string method, string route)
    {
        var api = Api.ActingAs(TestUser.Kai);
        var url = $"api/projects/{Ship}/{route}";

        using var response = method == "GET" ? await api.GetAsync(url) : await api.PostJsonAsync(url);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("POST")]
    [TestCase("DELETE")]
    public async Task ScreensRoutes_WhenNotAMemberOfThePointsProject_ShouldReturnNotFound(string method)
    {
        var api = Api.ActingAs(TestUser.Kai);
        var url = $"api/retros/points/{RetroSweepData.Point}/support";

        using var response = method == "POST" ? await api.PostJsonAsync(url) : await api.DeleteAsync(url);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}

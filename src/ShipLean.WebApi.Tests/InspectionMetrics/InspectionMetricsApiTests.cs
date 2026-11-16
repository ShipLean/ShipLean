using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// GET api/projects/{projectId}/inspection-metrics through the real app (SHIP-1398 K2, K3). Joel is a member of Ship and not of
// Part. The view's numbers fill in later lanes on the seeded database, so these tests pin the shape and the rules, not emptiness.
[TestFixture]
public class InspectionMetricsApiTests : ApiFixture
{
    private static readonly string[] ViewKeys =
    [
        "project", "cadence", "range", "cut", "aggregate", "generatedAt", "caveat", "coverage", "headline", "periods", "quarters",
        "cuts", "longest", "leftOut", "periodDetail", "quality", "bugs",
    ];

    private static readonly string[] QualityKeys = ["escaping", "weighted", "foundBeforeRelease", "mergedPrs", "per100Prs", "escapingBySeverity", "changePctVsPrevious"];

    private static readonly string[] SeverityKeys = ["sev1", "sev2", "sev3", "sev4", "unset"];

    private static string Url(Guid projectId, string query = "") => $"api/projects/{projectId}/inspection-metrics{query}";

    // Mutation: drop Caveat from InspectionMetricsViewDto.
    [Test]
    public async Task Get_WhenMember_ShouldReturnViewInFinalShape()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).ShouldBe(ViewKeys, ignoreOrder: true);
        root.GetProperty("caveat").GetString().ShouldBe("These numbers tell you where to look, not what caused a change.");
        root.GetProperty("project").GetProperty("key").GetString().ShouldBe("SHIP");
        root.GetProperty("cadence").GetString().ShouldBe("sprints");
        root.GetProperty("aggregate").GetString().ShouldBe("mean");
        root.GetProperty("range").GetString().ShouldBe("last6");
        root.GetProperty("cut").GetString().ShouldBe("none");
        root.GetProperty("cuts").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("periodDetail").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("periods").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    // SHIP-1398 QK3: quality on the view, every period, quarter and headlined sprint, and quality and bugs on the opened period. The
    // seeded project has live bugs, so the view, every period and the opened period carry a quality with exactly the six keys, and its
    // severity split the five; the range's and the period's bug lists are arrays. Headlined quarters and sprints are checked when set.
    // Mutations: drop Quality from InspectionPeriodDto; drop Bugs from InspectionPeriodDetailDto; drop Per100Prs; leave Quality null.
    [Test]
    public async Task Get_ShouldIncludeQualityInFinalShape()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, $"?period={SeedIds.Sprints.Sprint20}"));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        var headline = root.GetProperty("headline");
        var detail = root.GetProperty("periodDetail");
        JsonElement[] owners =
        [
            root, detail, .. root.GetProperty("periods").EnumerateArray(), .. root.GetProperty("quarters").EnumerateArray(),
            .. new[] { "finished", "soFar", "quarterToDate", "lastQuarter" }.Select(headline.GetProperty).Where(h => h.ValueKind != JsonValueKind.Null),
        ];
        root.GetProperty("periods").GetArrayLength().ShouldBeGreaterThan(0);
        foreach (var owner in owners)
        {
            var quality = owner.GetProperty("quality");
            quality.ValueKind.ShouldBe(JsonValueKind.Object);
            quality.EnumerateObject().Select(p => p.Name).ShouldBe(QualityKeys, ignoreOrder: true);
            quality.GetProperty("escapingBySeverity").EnumerateObject().Select(p => p.Name).ShouldBe(SeverityKeys, ignoreOrder: true);
        }
        detail.GetProperty("bugs").ValueKind.ShouldBe(JsonValueKind.Array);
        root.GetProperty("bugs").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task Get_WhenRangeAndCutGiven_ShouldEchoThem()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, "?range=lastQuarter&cut=who"));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("range").GetString().ShouldBe("lastQuarter");
        json.RootElement.GetProperty("cut").GetString().ShouldBe("who");
        json.RootElement.GetProperty("cuts").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    // Mutation: check access on a project the caller belongs to instead of the requested one.
    [Test]
    public async Task Get_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Part));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Get_WhenNotMemberAndRangeUnknown_ShouldReturnNotFound()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Part, "?range=last7"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("?range=last7", "range")]
    [TestCase("?range=Last6", "range")]
    [TestCase("?cut=team", "cut")]
    public async Task Get_WhenRangeUnknown_ShouldReturnValidation(string query, string field)
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, query));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(field);
    }

    [TestCase("not-a-guid")]
    [TestCase("00000000-0000-0000-0000-000000000001")]
    public async Task Get_WhenPeriodNotASprint_ShouldReturnValidation(string period)
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, $"?period={period}"));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("period");
    }

    [Test]
    public async Task Get_WhenPeriodIsASprint_ShouldReturnOk()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, $"?period={SeedIds.Sprints.Sprint20}&select=SHIP-1"));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
    }

    [Test]
    public async Task Get_WhenSelectWithoutPeriod_ShouldReturnValidation()
    {
        using var response = await Api.GetAsync(Url(SeedIds.Projects.Ship, "?select=SHIP-1"));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("select");
    }
}

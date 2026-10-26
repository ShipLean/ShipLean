using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views.Search;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Search.Journeys;

namespace ShipLean.WebApi.Tests.Search.Endpoint;

[TestFixture]
public class SearchEndpointApiTests : ApiFixture
{
    [Test]
    public async Task Search_WhenNoHits_ShouldReturnTheViewModelShape()
    {
        using var response = await Api.GetAsync("api/search?q=zzqxnomatchzz");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var view = JsonSerializer.Deserialize<SearchViewDto>(text, PersistedJson.Options)!;
        view.Query.ShouldBe("zzqxnomatchzz");
        view.Items.ShouldBeEmpty();
        view.Sprints.ShouldBeEmpty();
        view.Projects.ShouldBeEmpty();
        view.More.ShouldBeFalse();
        using var json = JsonDocument.Parse(text);
        json.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["query", "semantic", "projects", "sprints", "items", "more"]);
    }

    [Test]
    public async Task Search_WhenProjectNotMine_ShouldBeNotFound()
    {
        var error = await ErrorAsync("api/search?q=login&project=PART", HttpStatusCode.NotFound);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Search_WhenProjectUnknown_ShouldBeNotFound()
    {
        var error = await ErrorAsync("api/search?q=login&project=ZZZ", HttpStatusCode.NotFound);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Search_WhenQueryEmpty_ShouldBeValidation()
    {
        var error = await ErrorAsync("api/search?q=%20", HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("0")]
    [TestCase("26")]
    public async Task Search_WhenLimitOutOfRange_ShouldBeValidation(string limit)
    {
        var error = await ErrorAsync($"api/search?q=login&limit={limit}", HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Search_WhenTypeUnknown_ShouldBeValidation()
    {
        var error = await ErrorAsync("api/search?q=login&types=Story,Widget", HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("types=,")]
    [TestCase("types=%20,%20")]
    public async Task Search_WhenTypesHasOnlyBlankEntries_ShouldSearchWithoutATypeFilter(string types)
    {
        using var response = await Api.GetAsync($"api/search?q=login&{types}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Search_WhenItemAndSprintMatch_ShouldSendStateCategoryAndSprintStatus()
    {
        // The item is moved to Active (inProgress) and the sprint started three days ago, so neither field can pass at its default
        // (proposed, past).
        var item = await JourneyApi.CreateItemAsync(Api, SeedIds.Projects.Ship, "Okapi harness calibration");
        using var moved = await Api.PatchJsonAsync(
            $"api/work-items/{item.Id}",
            new JsonObject { ["expectedRevision"] = item.Revision, ["changes"] = new JsonObject { ["state"] = new JsonObject { ["value"] = SeedIds.States.Ship.StoryActive.ToString() } } });
        moved.StatusCode.ShouldBe(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());
        await JourneyApi.CreateSprintAsync(Api, SeedIds.Projects.Ship, "Sprint Okapi", "Okapi harness rollout", -3);
        await JourneyApi.ReindexAsync(Api);

        using var response = await Api.GetAsync("api/search?q=okapi&prefix=true&closed=true");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        var hit = json.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == item.Key);
        hit.GetProperty("stateCategory").GetString().ShouldBe("inProgress");
        var sprint = json.RootElement.GetProperty("sprints").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Sprint Okapi");
        sprint.GetProperty("status").GetString().ShouldBe("current");
        sprint.GetProperty("datesLabel").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    private async Task<ApiError> ErrorAsync(string url, HttpStatusCode status)
    {
        using var response = await Api.GetAsync(url);

        response.StatusCode.ShouldBe(status);
        return await response.ReadApiErrorAsync();
    }
}

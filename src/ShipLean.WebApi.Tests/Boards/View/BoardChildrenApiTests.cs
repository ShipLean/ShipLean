using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Boards.View;

[TestFixture]
public class BoardChildrenApiTests : ApiFixture
{
    private async Task<IReadOnlyList<ChildSummaryDto>> ChildrenAsync(Guid id, string query = "")
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/children{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<List<ChildSummaryDto>>(text, PersistedJson.Options)!;
    }

    [Test]
    public async Task Children_WhenFeatureHasStoriesAndBugs_ShouldListOpenFirstThenClosedWithStates()
    {
        var children = await ChildrenAsync(SeedIds.WorkItems.Ship101);

        var firstClosed = children.ToList().FindIndex(c => c.State.Category == StateCategory.Completed);
        firstClosed.ShouldBeGreaterThan(0);
        children.Take(firstClosed).ShouldAllBe(c => c.State.Category != StateCategory.Completed);
        children.Skip(firstClosed).ShouldAllBe(c => c.State.Category == StateCategory.Completed);
        children.Select(c => c.Key).ShouldContain("SHIP-141");
        children.Single(c => c.Key == "SHIP-141").State.Name.ShouldBe("Active");
    }

    [Test]
    public async Task Children_WhenEpic_ShouldListDescendantsAtEveryDepth()
    {
        var children = await ChildrenAsync(SeedIds.WorkItems.Ship92);

        children.Select(c => c.Key).ShouldContain("SHIP-103");
        children.Select(c => c.Key).ShouldContain("SHIP-108");
        children.Select(c => c.Key).ShouldContain("SHIP-147");
    }

    [Test]
    public async Task Children_WhenTypeGiven_ShouldKeepOnlyThatType()
    {
        var features = await ChildrenAsync(SeedIds.WorkItems.Ship92, "?type=feature");

        features.Select(c => c.Key).ShouldBe(["SHIP-103", "SHIP-108"], ignoreOrder: true);
        features.ShouldAllBe(c => c.Type.Id == WorkItemTypeId.Feature);
    }

    [Test]
    public async Task Children_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/work-items/{SeedIds.WorkItems.Ship101}/children");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Children_WhenUnknownItem_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"api/work-items/{Guid.NewGuid()}/children");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}

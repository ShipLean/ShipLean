using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// The New Feature dialog's slug check through the real endpoint: taken (any case), invalid with a reason, free, access first.
[TestFixture]
public class FeatureSlugCheckTests : ApiFixture
{
    private static readonly string Route = $"api/projects/{SeedIds.Projects.Ship}/features/slug-check";

    private async Task<SlugCheckDto> CheckAsync(string value)
    {
        using var response = await Api.GetAsync($"{Route}?value={Uri.EscapeDataString(value)}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<SlugCheckDto>(text, JsonSerializerOptions.Web)!;
    }

    [Test]
    public async Task SlugCheck_WhenTaken_ShouldReturnUnavailable()
    {
        using var created = await Api.PostJsonAsync(
            "api/work-items",
            new JsonObject { ["projectId"] = SeedIds.Projects.Ship, ["type"] = "feature", ["title"] = "Taken one", ["parentId"] = null, ["sprintId"] = null, ["changes"] = null, ["slug"] = "taken-one" });
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());

        var lower = await CheckAsync("taken-one");
        lower.Available.ShouldBeFalse();
        lower.Reason.ShouldBeNull();
        (await CheckAsync("free-one")).Available.ShouldBeTrue();
    }

    [Test]
    public async Task SlugCheck_WhenInvalid_ShouldReturnReason()
    {
        var upper = await CheckAsync("Not Valid");
        var empty = await CheckAsync("");
        var tooLong = await CheckAsync(new string('a', 65));

        foreach (var result in new[] { upper, empty, tooLong })
        {
            result.Available.ShouldBeFalse();
            result.Reason.ShouldNotBeNullOrEmpty();
        }

        upper.Value.ShouldBe("Not Valid");
    }

    [Test]
    public async Task SlugCheck_WhenNonMember_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"{Route}?value=anything");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}

using System.Net;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// SHIPLEAN_INSPECTION unset: every K5 route is not_found, even for the project's admin.
[TestFixture]
public class InspectionOffTests : ApiFixture
{
    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs")]
    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs/sess-1")]
    [TestCase("api/inspection/projects/SHIP/items/SHIP-1/runs/sess-1/rows/r1")]
    [TestCase("api/inspection/projects/SHIP/settings")]
    [TestCase("api/inspection/projects/SHIP/agent-setup?agent=claude-code")]
    public async Task Get_WhenInstanceOff_ShouldBeNotFound(string url)
    {
        using var response = await Api.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Health_WhenInstanceOff_ShouldSayDisabled()
    {
        using var response = await Api.GetAsync("api/inspection/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"enabled\":false");
    }
}
